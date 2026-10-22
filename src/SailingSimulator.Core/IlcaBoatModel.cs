namespace SailingSimulator.Core;

public sealed record BoatDiagnostics(ForceMoment Gravity, ForceMoment Buoyancy, ForceMoment HullDrag,
    ForceMoment Centerboard, ForceMoment Rudder, ForceMoment Sail, SailShapeState SailShape)
{
    public ForceMoment Total => Gravity + Buoyancy + HullDrag + Centerboard + Rudder + Sail;
}

/// <summary>Hull-datum locations; section angles/shape are exactly those used by aerodynamics.</summary>
public readonly record struct SailSectionState(double HeightM, double AreaM2, double ChordM,
    double AngleRad, double CamberRatio, double TwistRad, double Luffing,
    Vector3d CenterBodyM, Vector3d ApparentWindBodyMps, ForceMoment Force);

public sealed record SailShapeState(double BoomAngleRad, double SheetLimitRad, IReadOnlyList<SailSectionState> Sections);

public readonly record struct HullSample(Vector3d BottomBodyM, double HeightM, double WaterplaneAreaM2);
public readonly record struct HullForces(ForceMoment Buoyancy, ForceMoment Drag);

internal static class BoatGeometry
{
    public const double GravityMps2 = 9.80665;
    public const double WaterDensityKgM3 = 1000;
    public const double AirDensityKgM3 = 1.225;

    public static Vector3d WorldPoint(in ForceEvaluation e, Vector3d hullPoint) =>
        e.Boat.PositionWorldM + e.Boat.OrientationBodyToWorld.Rotate(hullPoint - e.Mass.CenterOfMassBodyM);

    public static Vector3d PointVelocityBody(in ForceEvaluation e, Vector3d hullPoint) =>
        e.Boat.OrientationBodyToWorld.Conjugate().Rotate(e.Boat.LinearVelocityWorldMps) +
        Vector3d.Cross(e.Boat.AngularVelocityBodyRadPerSec, hullPoint - e.Mass.CenterOfMassBodyM);

    public static ForceMoment AtPoint(in ForceEvaluation e, Vector3d forceBody, Vector3d hullPoint) =>
        ForceMoment.AtBodyPoint(forceBody, hullPoint - e.Mass.CenterOfMassBodyM, e.Boat.OrientationBodyToWorld);

    public static Vector3d WaterRelativeVelocityBody(in ForceEvaluation e, Vector3d hullPoint) =>
        PointVelocityBody(in e, hullPoint) - e.Boat.OrientationBodyToWorld.Conjugate().Rotate(
            e.Input.Environment.Water.VelocityAt(WorldPoint(in e, hullPoint), e.TimeSeconds));

    // Linearly interpolate signed endpoint depths to approximate one wet segment.
    public static (double Fraction, Vector3d Center) Submerged(in ForceEvaluation e, Vector3d top, Vector3d bottom)
    {
        var topWorld = WorldPoint(in e, top);
        var bottomWorld = WorldPoint(in e, bottom);
        var water = e.Input.Environment.Water;
        var a = topWorld.Z - water.SampleSurface(topWorld, e.TimeSeconds).PositionWorldZM;
        var b = bottomWorld.Z - water.SampleSurface(bottomWorld, e.TimeSeconds).PositionWorldZM;
        if (a <= 0 && b <= 0) return (0, (top + bottom) / 2);
        if (a >= 0 && b >= 0) return (1, (top + bottom) / 2);
        var fraction = Math.Max(a, b) / Math.Abs(b - a);
        return (fraction, a > b ? top + (bottom - top) * (fraction / 2)
            : bottom + (top - bottom) * (fraction / 2));
    }
}

public sealed class GravityModel : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation e) =>
        new(new(0, 0, e.Mass.MassKg * BoatGeometry.GravityMps2), Vector3d.Zero);
}

/// <summary>48 sealed buoyancy columns over a parametric waterplane, plus dissipative local drag.</summary>
public sealed class SampledHullModel : IForceModel
{
    public IReadOnlyList<HullSample> Samples { get; }

    public SampledHullModel()
    {
        var samples = new List<HullSample>();
        const int longitudinal = 12, transverse = 4;
        const double dx = 3.81 / longitudinal;
        for (var i = 0; i < longitudinal; i++)
        {
            var x = -3.81 / 2 + (i + 0.5) * dx;
            var halfBeam = 1.37 / 2 * Math.Sqrt(1 - Math.Pow(2 * x / 4.23, 2));
            var dy = 2 * halfBeam / transverse;
            for (var j = 0; j < transverse; j++)
            {
                var y = -halfBeam + (j + 0.5) * dy;
                var bottomZ = 0.12 - 0.10 * Math.Pow(x / 1.905, 2) - 0.06 * Math.Pow(y / halfBeam, 2);
                samples.Add(new(new(x, y, bottomZ), 0.32, dx * dy));
            }
        }
        Samples = samples.AsReadOnly();
    }

    public ForceMoment Evaluate(in ForceEvaluation e)
    {
        var forces = EvaluateHull(in e);
        return forces.Buoyancy + forces.Drag;
    }

    public HullForces EvaluateHull(in ForceEvaluation e)
    {
        var buoyancy = default(ForceMoment);
        var drag = default(ForceMoment);
        foreach (var sample in Samples)
        {
            var top = sample.BottomBodyM - Vector3d.UnitZ * sample.HeightM;
            var (fraction, center) = BoatGeometry.Submerged(in e, top, sample.BottomBodyM);
            var upwardWorld = new Vector3d(0, 0, -BoatGeometry.WaterDensityKgM3 * BoatGeometry.GravityMps2 *
                sample.WaterplaneAreaM2 * sample.HeightM * fraction);
            buoyancy += BoatGeometry.AtPoint(in e, e.Boat.OrientationBodyToWorld.Conjugate().Rotate(upwardWorld), center);
            var v = BoatGeometry.WaterRelativeVelocityBody(in e, center);
            var wetArea = sample.WaterplaneAreaM2 * Math.Min(1, fraction * sample.HeightM / 0.05);
            var force = new Vector3d(
                -wetArea * (0.5 * BoatGeometry.WaterDensityKgM3 * 0.012 * v.X * Math.Abs(v.X) + 0.4 * v.X),
                // Effective shallow-hull side projection is much smaller than waterplane area.
                -wetArea * (0.5 * BoatGeometry.WaterDensityKgM3 * 0.08 * v.Y * Math.Abs(v.Y) + 5 * v.Y),
                -wetArea * (0.5 * BoatGeometry.WaterDensityKgM3 * 0.80 * v.Z * Math.Abs(v.Z) + 30 * v.Z));
            drag += BoatGeometry.AtPoint(in e, force, center);
        }
        return new(buoyancy, drag);
    }
}

public enum FoilKind { Centerboard, Rudder }

/// <summary>Independent wet-area lift/drag sources, including local rotational flow and stall.</summary>
public sealed class FoilModel(FoilKind kind) : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation e)
    {
        var lowering = kind == FoilKind.Centerboard ? e.Input.Controls.Centerboard : 1;
        if (lowering == 0) return default;
        var span = (kind == FoilKind.Centerboard ? 0.75 : 0.48) * lowering;
        var top = new Vector3d(kind == FoilKind.Centerboard ? 0.05 : -2.05, 0, 0.08);
        var (wet, center) = BoatGeometry.Submerged(in e, top, top + Vector3d.UnitZ * span);
        var area = (kind == FoilKind.Centerboard ? 0.24 : 0.10) * lowering * wet;
        // Positive tiller = rudder trailing edge to starboard, turning bow to starboard in forward flow.
        var delta = kind == FoilKind.Rudder ? -0.45 * e.Input.Controls.Tiller : 0;
        var chord = new Vector3d(Math.Cos(delta), Math.Sin(delta), 0);
        var normal = new Vector3d(-Math.Sin(delta), Math.Cos(delta), 0);
        var velocity = BoatGeometry.WaterRelativeVelocityBody(in e, center);
        var u = Vector3d.Dot(velocity, chord);
        var v = Vector3d.Dot(velocity, normal);
        var speed = Math.Sqrt(u * u + v * v);
        if (speed < 1e-10) return BoatGeometry.AtPoint(in e,
            new(0, 0, -0.5 * BoatGeometry.WaterDensityKgM3 * area * 0.08 * velocity.Z * Math.Abs(velocity.Z)), center);
        var alpha = Math.Atan2(v, u);
        var cl = 1.1 * Math.Sin(2 * alpha);
        var cd = 0.012 + 0.09 * cl * cl + 0.8 * Math.Pow(Math.Sin(alpha), 2);
        var q = 0.5 * BoatGeometry.WaterDensityKgM3 * area * speed * speed;
        var force = chord * (q * (cl * v - cd * u) / speed) + normal * (q * (-cl * u - cd * v) / speed)
            + new Vector3d(0, 0, -0.5 * BoatGeometry.WaterDensityKgM3 * area * 0.08 * velocity.Z * Math.Abs(velocity.Z));
        return BoatGeometry.AtPoint(in e, force, center);
    }
}

/// <summary>Six sail strips sharing a dynamic boom, with trim-dependent camber/twist and local flow.</summary>
public sealed class SectionalSailModel : IForceModel
{
    public const int SectionCount = 6;
    public double SheetLimit(SailingControls c) => 0.08 + 1.35 * (1 - c.Mainsheet) + 0.20 * (1 - c.Traveler);

    public ForceMoment Evaluate(in ForceEvaluation e) => EvaluateSail(in e, null).Force;

    public (ForceMoment Force, double BoomTorqueNm) EvaluateSail(in ForceEvaluation e, SailSectionState[]? sections)
    {
        var total = default(ForceMoment);
        var boomTorque = 0.0;
        var c = e.Input.Controls;
        var camber = 0.045 + 0.085 * (1 - c.Outhaul) * (1 - 0.45 * c.Cunningham);
        var twist = (0.06 + 0.42 * (1 - c.Vang) + 0.10 * (1 - c.Cunningham)) * Math.Tanh(e.Rig.BoomAngleRad / 0.12);
        const double height = 5.2, area = 7.06, mastX = 0.65;
        for (var i = 0; i < SectionCount; i++)
        {
            var fraction = (i + 0.5) / SectionCount;
            // Linear taper integrates exactly to 7.06 m² over the six equal-height strips.
            var chordLength = 2 * area / height * (1 - fraction);
            var angle = e.Rig.BoomAngleRad + twist * fraction;
            var chord = new Vector3d(-Math.Cos(angle), Math.Sin(angle), 0);
            var normal = new Vector3d(Math.Sin(angle), Math.Cos(angle), 0);
            var chordOffset = chord * (0.4 * chordLength);
            var center = new Vector3d(mastX, 0, -0.5 - height * fraction) + chordOffset;
            var boomPointVelocity = new Vector3d(-chordOffset.Y, chordOffset.X, 0) * -e.Rig.BoomRateRadPerSec;
            var wind = e.Boat.OrientationBodyToWorld.Conjugate().Rotate(e.Input.Environment.TrueWindVelocityWorldMps)
                - BoatGeometry.PointVelocityBody(in e, center) - boomPointVelocity;
            var speed = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
            var alpha = Math.Atan2(Vector3d.Dot(wind, normal), Vector3d.Dot(wind, chord));
            var luff = Math.Exp(-Math.Pow(Math.Sin(alpha) / 0.12, 2));
            var force = Vector3d.Zero;
            var sectionArea = chordLength * height / SectionCount;
            if (speed > 1e-10)
            {
                var attached = 1 - luff;
                var cl = (1.1 + 1.8 * camber) * Math.Sin(2 * alpha) * attached;
                var cd = (0.025 + 0.12 * camber + 1.35 * Math.Pow(Math.Sin(alpha), 2)) * attached;
                var q = 0.5 * BoatGeometry.AirDensityKgM3 * sectionArea * speed * speed;
                force = new Vector3d(wind.Y * cl + wind.X * cd, -wind.X * cl + wind.Y * cd, 0) * (q / speed);
            }
            var contribution = BoatGeometry.AtPoint(in e, force, center);
            total += contribution;
            boomTorque -= Vector3d.Cross(chordOffset, force).Z;
            if (sections is not null) sections[i] = new(height * fraction, sectionArea, chordLength, angle, camber,
                twist * fraction, luff, center, wind, contribution);
        }
        return (total, boomTorque);
    }

    public SailShapeState Shape(in ForceEvaluation e)
    {
        var sections = new SailSectionState[SectionCount];
        EvaluateSail(in e, sections);
        return new(e.Rig.BoomAngleRad, SheetLimit(e.Input.Controls), Array.AsReadOnly(sections));
    }
}

/// <summary>Uncalibrated ILCA 7 model with sampled local wave elevation and water-relative flow.</summary>
public sealed class IlcaBoatModel : IRigDynamicsModel, IBoatDiagnosticsModel
{
    public GravityModel Gravity { get; } = new();
    public SampledHullModel Hull { get; } = new();
    public FoilModel Centerboard { get; } = new(FoilKind.Centerboard);
    public FoilModel Rudder { get; } = new(FoilKind.Rudder);
    public SectionalSailModel Sail { get; } = new();
    public RigState InitialRig => new(0.1, 0);

    public ForceMoment Evaluate(in ForceEvaluation e) => EvaluateDynamics(in e).Forces;

    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation e)
    {
        var hull = Hull.EvaluateHull(in e);
        var sail = Sail.EvaluateSail(in e, null);
        var angle = e.Rig.BoomAngleRad;
        var limit = Sail.SheetLimit(e.Input.Controls);
        var sheetTorque = -1500 * (Math.Max(0, angle - limit) + Math.Min(0, angle + limit));
        var boomAcceleration = (sail.BoomTorqueNm + sheetTorque - (18 + 12 * e.Input.Controls.Vang) * e.Rig.BoomRateRadPerSec) / 10;
        return (Gravity.Evaluate(in e) + hull.Buoyancy + hull.Drag + Centerboard.Evaluate(in e) + Rudder.Evaluate(in e) + sail.Force,
            new(e.Rig.BoomRateRadPerSec, boomAcceleration));
    }

    public BoatDiagnostics Inspect(in ForceEvaluation e)
    {
        var hull = Hull.EvaluateHull(in e);
        var shape = Sail.Shape(in e);
        var sailForce = default(ForceMoment);
        foreach (var section in shape.Sections) sailForce += section.Force;
        var result = new BoatDiagnostics(Gravity.Evaluate(in e), hull.Buoyancy, hull.Drag,
            Centerboard.Evaluate(in e), Rudder.Evaluate(in e), sailForce, shape);
        if (!result.Total.IsFinite || shape.Sections.Any(s => !s.ApparentWindBodyMps.IsFinite || !double.IsFinite(s.Luffing)))
            throw new ArithmeticException("Non-finite boat diagnostics.");
        return result;
    }

    /// <summary>Upright instantaneous heave balance at t=0; wave motion and pitch/roll settle dynamically.</summary>
    public RigidBodyState CreateInitialState(SimulationInput input, double headingRad = 0)
    {
        var mass = new IlcaMassModel().Evaluate(input.Controls);
        var orientation = Quaterniond.FromAxisAngle(Vector3d.UnitZ, headingRad);
        var lower = -1.0;
        var upper = 1.0;
        for (var i = 0; i < 60; i++)
        {
            var z = (lower + upper) / 2;
            var boat = new RigidBodyState(new(0, 0, z), orientation, default, default);
            var force = Hull.EvaluateHull(new(0, boat, mass, input)).Buoyancy.ForceWorldN.Z + mass.MassKg * BoatGeometry.GravityMps2;
            if (force > 0) lower = z; else upper = z;
        }
        return new(new(0, 0, (lower + upper) / 2), orientation, default, default);
    }
}
