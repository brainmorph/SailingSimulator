namespace SailingSimulator.Core;

/// <summary>Provides a Hunter sail evaluation for a completed simulation snapshot.</summary>
public interface IHunterRigInspectionModel : IForceModel
{
    HunterRigEvaluation InspectHunterRig(in ForceEvaluation evaluation);
    HunterBoatGeometry InspectHunterGeometry(SailingControls controls);
}

/// <summary>One aerodynamic section's hull-datum leading and trailing points, in metres.</summary>
public readonly record struct HunterSailSectionGeometry(Vector3d LeadingEdgeBodyM, Vector3d TrailingEdgeBodyM);

/// <summary>
/// A completed evaluation of one Hunter sail. Sections are immutable and owned by the result.
/// Induced airflow is the other sail's contribution already included in each section's apparent wind.
/// </summary>
public sealed record HunterSailState(string Id, double PortLimitRad, double StarboardLimitRad,
    IReadOnlyList<SailSectionState> Sections, IReadOnlyList<HunterSailSectionGeometry> Geometry,
    Vector3d TackBodyM, Vector3d HeadBodyM, Vector3d ClewBodyM, ForceMoment Force, double TorqueNm,
    IReadOnlyList<Vector3d> InducedAirflowBodyMps);

/// <summary>
/// Completed sail state. With experimental sheet geometry, the jib's legacy
/// angular limits are informational only and do not describe its sheet forces.
/// </summary>
public sealed record HunterRigEvaluation(HunterSailState Main, HunterSailState Jib, RigDerivative Derivative,
    bool ExperimentalSheetGeometry = false, HunterSheetEvaluation? PortSheet = null, HunterSheetEvaluation? StarboardSheet = null,
    bool ExperimentalRigMomentum = false)
{
    public ForceMoment Total => Main.Force + Jib.Force;
}

/// <summary>Independent-sail prototype; coefficients, limits and inertia are not calibrated.</summary>
public sealed class Hunter235RigModel : IRigDynamicsModel
{
    private readonly bool experimentalSheetGeometry;
    private readonly bool sailInteraction;
    /// <summary>
    /// The Hunter definition selects sheet geometry and sail interaction; the legacy constraint
    /// and independent sails remain for comparison checks.
    /// </summary>
    public Hunter235RigModel(bool experimentalSheetGeometry = false, bool sailInteraction = false)
    {
        this.experimentalSheetGeometry = experimentalSheetGeometry;
        this.sailInteraction = sailInteraction;
    }
    private const int SectionCount = 6;
    internal const double BoomInertiaKgM2 = 120;
    internal const double JibInertiaKgM2 = 40;
    private static readonly Vector3d JibTack = new(3.0908, 0, -0.45);
    private static readonly Vector3d JibHead = new(0.5, 0, -8.2224);
    private static readonly Vector3d ForestayAxis = (JibHead - JibTack) / (JibHead - JibTack).Length;
    public RigState InitialRig => new(0.1, 0, 0.1, 0);

    public ForceMoment Evaluate(in ForceEvaluation e) => EvaluateDynamics(in e).Forces;

    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e)
    {
        var result = Inspect(in e);
        return (result.Total, result.Derivative);
    }

    public HunterRigEvaluation Inspect(in ForceEvaluation e)
    {
        var controls = e.Input.Controls;
        var mainLimit = 0.08 + 1.35 * (1 - controls.Mainsheet) + 0.20 * (1 - controls.Traveler);
        // A sheet on one side restrains the clew opening toward the opposite side.
        var portLimit = 0.15 + 1.3 * (1 - controls.StarboardJibSheet);
        var starboardLimit = 0.15 + 1.3 * (1 - controls.PortJibSheet);
        Span<Vector3d> mainInduced = stackalloc Vector3d[SectionCount];
        Span<Vector3d> jibInduced = stackalloc Vector3d[SectionCount];
        if (sailInteraction)
        {
            // One explicit pass: isolated circulation of each sail induces flow at the other.
            for (var i = 0; i < SectionCount; i++)
            {
                var mainSection = Section(in e, false, i, Vector3d.Zero);
                var jibSection = Section(in e, true, i, Vector3d.Zero);
                mainInduced[i] = Induced(jibSection, mainSection.Center);
                jibInduced[i] = Induced(mainSection, jibSection.Center);
            }
        }
        var main = Sail(in e, false, mainLimit, mainLimit, mainInduced);
        var jib = Sail(in e, true, portLimit, starboardLimit, jibInduced);
        static double Constraint(double angle, double port, double starboard, double stiffness) =>
            -stiffness * (Math.Max(0, angle - starboard) + Math.Min(0, angle + port));
        var boomAcceleration = (main.TorqueNm + Constraint(e.Rig.BoomAngleRad, mainLimit, mainLimit, 2500)
            - (100 + 50 * controls.Vang) * e.Rig.BoomRateRadPerSec) / BoomInertiaKgM2;
        HunterSheetEvaluation? portSheet = experimentalSheetGeometry
            ? Hunter235SheetExperiment.Evaluate(true, controls.PortJibSheet, e.Rig.JibAngleRad) : null;
        HunterSheetEvaluation? starboardSheet = experimentalSheetGeometry
            ? Hunter235SheetExperiment.Evaluate(false, controls.StarboardJibSheet, e.Rig.JibAngleRad) : null;
        var sheetTorque = experimentalSheetGeometry
            ? portSheet!.Value.TorqueAboutForestayNm + starboardSheet!.Value.TorqueAboutForestayNm
            : Constraint(e.Rig.JibAngleRad, portLimit, starboardLimit, 1000);
        var jibAcceleration = (jib.TorqueNm + sheetTorque
            - 80 * e.Rig.JibRateRadPerSec) / JibInertiaKgM2;
        return new(main, jib, new(e.Rig.BoomRateRadPerSec, boomAcceleration, e.Rig.JibRateRadPerSec, jibAcceleration),
            experimentalSheetGeometry, portSheet, starboardSheet);
    }

    private readonly record struct SectionFlow(Vector3d Pivot, Vector3d Chord, double ChordLength, Vector3d Offset,
        Vector3d Center, Vector3d Wind, double Area, double Height, double SectionAngle, double Camber, double Twist,
        double Luff, Vector3d Force, Vector3d Circulation);

    // Strip interaction: the inducing section is a 2D bound vortex at quarter chord.
    // The core radius is an estimate that keeps the induced speed finite near the slot.
    private static Vector3d Induced(in SectionFlow source, Vector3d point)
    {
        var vortex = source.Pivot + source.Chord * (0.25 * source.ChordLength);
        var d = new Vector3d(point.X - vortex.X, point.Y - vortex.Y, 0);
        var core = Math.Max(0.1, 0.25 * source.ChordLength);
        return Vector3d.Cross(source.Circulation, d) / (2 * Math.PI * (d.LengthSquared + core * core));
    }

    private static SectionFlow Section(in ForceEvaluation e, bool jib, int i, Vector3d induced)
    {
        var c = e.Input.Controls;
        var camber = jib ? 0.08 : 0.045 + 0.085 * (1 - c.Outhaul) * (1 - 0.45 * c.Cunningham);
        var angle = jib ? e.Rig.JibAngleRad : e.Rig.BoomAngleRad;
        var rate = jib ? e.Rig.JibRateRadPerSec : e.Rig.BoomRateRadPerSec;
        var axis = jib ? ForestayAxis : -Vector3d.UnitZ;
        var twist = jib ? 0 : (0.06 + 0.42 * (1 - c.Vang) + 0.10 * (1 - c.Cunningham)) * Math.Tanh(angle / 0.12);
        var height = jib ? 7.7724 : 7.8232;
        var f = (i + 0.5) / SectionCount;
        var sectionAngle = angle + twist * f;
        var chordLength = jib ? 2.1679 * (1 - f) : 3.048 * (1 - f) + 0.3024 * 4 * f * (1 - f);
        var pivot = jib ? JibTack + (JibHead - JibTack) * f : new Vector3d(0.5, 0, -0.65 - height * f);
        var chord = Quaterniond.FromAxisAngle(axis, sectionAngle).Rotate(new(-1, 0, 0));
        var offset = chord * (0.4 * chordLength);
        var center = pivot + offset;
        var wind = e.Boat.OrientationBodyToWorld.Conjugate().Rotate(e.Input.Environment.TrueWindVelocityWorldMps)
            - BoatGeometry.PointVelocityBody(in e, center) - Vector3d.Cross(axis, offset) * rate + induced;
        var horizontalChord = new Vector3d(chord.X, chord.Y, 0);
        horizontalChord /= horizontalChord.Length;
        var normal = new Vector3d(horizontalChord.Y, -horizontalChord.X, 0);
        var speed = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
        var alpha = Math.Atan2(Vector3d.Dot(wind, normal), Vector3d.Dot(wind, horizontalChord));
        var luff = Math.Exp(-Math.Pow(Math.Sin(alpha) / 0.12, 2));
        var area = chordLength * height / SectionCount;
        var force = Vector3d.Zero;
        var circulation = Vector3d.Zero;
        if (speed > 1e-10)
        {
            var cl = (1.1 + 1.8 * camber) * Math.Sin(2 * alpha) * (1 - luff);
            var cd = (0.025 + 0.12 * camber + 1.35 * Math.Pow(Math.Sin(alpha), 2)) * (1 - luff);
            var q = 0.5 * BoatGeometry.AirDensityKgM3 * area * speed * speed;
            force = new Vector3d(wind.Y * cl + wind.X * cd, -wind.X * cl + wind.Y * cd, 0) * (q / speed);
            // Kutta-Joukowski: lift per span = rho W x Gamma, so Gamma = L' x W / (rho |W|^2).
            var liftPerSpan = new Vector3d(wind.Y, -wind.X, 0) * (cl * q / speed / (height / SectionCount));
            var horizontalWind = new Vector3d(wind.X, wind.Y, 0);
            circulation = Vector3d.Cross(liftPerSpan, horizontalWind) / (BoatGeometry.AirDensityKgM3 * speed * speed);
        }
        return new(pivot, chord, chordLength, offset, center, wind, area, height, sectionAngle, camber, twist * f,
            luff, force, circulation);
    }

    private static HunterSailState Sail(in ForceEvaluation e, bool jib, double portLimit, double starboardLimit,
        ReadOnlySpan<Vector3d> induced)
    {
        var sections = new SailSectionState[SectionCount];
        var geometry = new HunterSailSectionGeometry[SectionCount];
        var inducedAirflow = induced.ToArray();
        var total = default(ForceMoment);
        var torque = 0.0;
        var axis = jib ? ForestayAxis : -Vector3d.UnitZ;
        for (var i = 0; i < SectionCount; i++)
        {
            var s = Section(in e, jib, i, induced[i]);
            geometry[i] = new(s.Pivot, s.Pivot + s.Chord * s.ChordLength);
            var contribution = BoatGeometry.AtPoint(in e, s.Force, s.Center);
            total += contribution;
            torque += Vector3d.Dot(Vector3d.Cross(s.Offset, s.Force), axis);
            sections[i] = new(s.Height * ((i + 0.5) / SectionCount), s.Area, s.ChordLength, s.SectionAngle, s.Camber,
                s.Twist, s.Luff, s.Center, s.Wind, contribution);
        }
        var angle = jib ? e.Rig.JibAngleRad : e.Rig.BoomAngleRad;
        var height = jib ? 7.7724 : 7.8232;
        var tack = jib ? JibTack : new Vector3d(0.5, 0, -0.65);
        var head = jib ? JibHead : new Vector3d(0.5, 0, -0.65 - height);
        var foot = jib ? 2.1679 : 3.048;
        var clew = tack + Quaterniond.FromAxisAngle(axis, angle).Rotate(new(-foot, 0, 0));
        return new(jib ? "jib" : "main", portLimit, starboardLimit, Array.AsReadOnly(sections),
            Array.AsReadOnly(geometry), tack, head, clew, total, torque, Array.AsReadOnly(inducedAirflow));
    }
}
