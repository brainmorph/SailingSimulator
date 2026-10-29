using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

/// <summary>Presentation-only deformation of the published aerodynamic strips, in hull-datum NED.
/// No control-to-shape calculation or force evaluation occurs here.</summary>
public static class SailingVisuals
{
    public static Vector3d SailPoint(SailSectionState section, double chordFraction, double timeSeconds) =>
        SailPoint(Strip(section), chordFraction, timeSeconds);

    /// <summary>
    /// A cloth point on a published strip: the chord line, a leeward belly from the strip's camber and
    /// luff flutter. The leeward side follows the strip's local airflow, so a backwinded strip bellies
    /// the other way.
    /// </summary>
    public static Vector3d SailPoint(SailStrip strip, double chordFraction, double timeSeconds)
    {
        var section = strip.Section;
        var windAcross = Vector3d.Dot(section.ApparentWindBodyMps, strip.Normal);
        var side = windAcross < 0 ? -1 : 1;
        var speed = section.ApparentWindBodyMps.Length;
        var envelope = Math.Sin(Math.PI * chordFraction);
        var belly = side * section.CamberRatio * strip.ChordM * 4 * chordFraction * (1 - chordFraction);
        // Luffing changes displacement, never the aerodynamic angle/force; no wind means no flutter.
        var flutter = section.Luffing * Math.Min(1, speed / 2) * strip.ChordM * 0.11 * envelope *
            Math.Sin(timeSeconds * 24 + section.HeightM * 5 + chordFraction * 13);
        return strip.LeadingEdgeBodyM + strip.Chord * (strip.ChordM * chordFraction) + strip.Normal * (belly + flutter);
    }

    /// <summary>An ILCA strip: the luff is on the vertical mast and the chord is horizontal at the strip angle.</summary>
    public static SailStrip Strip(SailSectionState section)
    {
        var chord = new Vector3d(-Math.Cos(section.AngleRad), Math.Sin(section.AngleRad), 0);
        return new(section, section.CenterBodyM - chord * (0.4 * section.ChordM), chord, section.ChordM);
    }

    /// <summary>
    /// The Hunter's published strips with foot and head rows: the foot from tack to clew and a
    /// zero-length head, both using the nearest strip's airflow.
    /// </summary>
    public static SailStrip[] HunterStrips(HunterSailState sail)
    {
        var strips = new SailStrip[sail.Sections.Count + 2];
        for (var i = 0; i < sail.Sections.Count; i++)
        {
            var g = sail.Geometry[i];
            var span = g.TrailingEdgeBodyM - g.LeadingEdgeBodyM;
            strips[i + 1] = new(sail.Sections[i], g.LeadingEdgeBodyM, span / span.Length, span.Length);
        }
        var foot = sail.ClewBodyM - sail.TackBodyM;
        strips[0] = new(sail.Sections[0] with { HeightM = 0 }, sail.TackBodyM, foot / foot.Length, foot.Length);
        strips[^1] = new(sail.Sections[^1], sail.HeadBodyM, strips[^2].Chord, 0);
        return strips;
    }

    /// <summary>Extend the first/last strip to foot/head; retain every published strip at its exact height.</summary>
    public static SailSectionState EdgeSection(SailShapeState shape, bool head)
    {
        var a = head ? shape.Sections[^1] : shape.Sections[0];
        var b = head ? shape.Sections[^2] : shape.Sections[1];
        var height = head ? SectionalSailModel.HeightM : 0;
        var t = (height - a.HeightM) / (b.HeightM - a.HeightM);
        var angle = a.AngleRad + t * (b.AngleRad - a.AngleRad);
        var chord = head ? 0 : a.ChordM + t * (b.ChordM - a.ChordM);
        return a with
        {
            HeightM = height, ChordM = chord, AngleRad = angle,
            CenterBodyM = new Vector3d(SectionalSailModel.MastXM, 0, -SectionalSailModel.FootHeightM - height) +
                new Vector3d(-Math.Cos(angle), Math.Sin(angle), 0) * (0.4 * chord)
        };
    }

    public static double Separation(SailSectionState section) => Separation(Strip(section));

    public static double Separation(SailStrip strip)
    {
        var wind = strip.Section.ApparentWindBodyMps;
        var speed = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
        if (speed < 0.1) return 0;
        var across = Vector3d.Dot(wind, strip.Normal);
        // A visible high-incidence cue, not a measured boundary-layer model.
        return Math.Clamp((Math.Abs(across) / speed - 0.35) / 0.45, 0, 1);
    }

    public static Vector3d TelltalePoint(SailSectionState section, int side, double along, double timeSeconds) =>
        TelltalePoint(Strip(section), side, along, timeSeconds);

    /// <summary>A ribbon on one side of the cloth at the given chord fraction, streaming in the strip's local airflow.</summary>
    public static Vector3d TelltalePoint(SailStrip strip, int side, double along, double timeSeconds, double chordFraction = 0.27)
    {
        var section = strip.Section;
        var chord = strip.Chord;
        var normal = strip.Normal;
        var wind = section.ApparentWindBodyMps;
        var speed = wind.Length;
        var anchor = SailPoint(strip, chordFraction, timeSeconds) + normal * (side * 0.018);
        if (speed < 0.1) return anchor + Vector3d.UnitZ * (along * 0.27);
        var flowAlong = Vector3d.Dot(wind, chord);
        var flowAcross = Vector3d.Dot(wind, normal);
        var leeward = flowAcross * side > 0;
        var disorder = Math.Max(section.Luffing, Separation(strip) * (leeward ? 1 : 0.35));
        var phase = timeSeconds * 28 + section.HeightM * 7 + side * 2 + along * 10;
        var tangent = chord * (flowAlong < 0 ? -1 : 1);
        var stream = tangent * (along * 0.30 * (1 - 0.55 * disorder));
        var flutter = normal * (disorder * along * 0.12 * Math.Sin(phase)) +
            Vector3d.UnitZ * (along * (0.03 + disorder * 0.14 * Math.Cos(phase * 0.83)));
        return anchor + stream + flutter;
    }

    /// <summary>Exact spectral inputs for the water vertex shader: direction in Godot X/Z, amplitude, k.</summary>
    public static WaterVisualMode WaterMode(WaveComponent component) => new(
        component.TravelDirectionWorld.Y, -component.TravelDirectionWorld.X,
        component.AmplitudeM, component.WavenumberRadPerM,
        component.AngularFrequencyRadPerSec, component.PhaseRad);
}

/// <summary>One sail strip in hull-datum NED: luff point, unit chord direction and chord length.</summary>
public readonly record struct SailStrip(SailSectionState Section, Vector3d LeadingEdgeBodyM, Vector3d Chord, double ChordM)
{
    /// <summary>The horizontal normal used for the strip's angle of attack; starboard for a centred chord.</summary>
    public Vector3d Normal
    {
        get
        {
            var length = Math.Sqrt(Chord.X * Chord.X + Chord.Y * Chord.Y);
            return new(Chord.Y / length, -Chord.X / length, 0);
        }
    }
}

public readonly record struct WaterVisualMode(double DirectionX, double DirectionZ, double AmplitudeM,
    double Wavenumber, double Frequency, double Phase);

public readonly record struct WakeSample(Vector3d PositionWorldM, Vector3d SideWorld, double TimeSeconds, double Strength);

/// <summary>Bounded, simulation-time wake history. Render frame rate cannot create extra wake.</summary>
public sealed class WakeTrail
{
    public const int Capacity = 80;
    public const double LifetimeSeconds = 7;
    private readonly Queue<WakeSample> samples = new(Capacity);
    private double lastTime = double.NegativeInfinity;
    public IReadOnlyCollection<WakeSample> Samples => samples;

    public void Clear()
    {
        samples.Clear();
        lastTime = double.NegativeInfinity;
    }

    public void Update(SimulationState state)
    {
        while (samples.TryPeek(out var first) && state.TimeSeconds - first.TimeSeconds > LifetimeSeconds) samples.Dequeue();
        if (state.TimeSeconds - lastTime < 0.1) return;
        var q = state.Boat.OrientationBodyToWorld;
        // Boats that publish geometry supply their own stern; the ILCA stern is at -2.0 m.
        var sternBody = state.HunterGeometry?.SternBodyM ?? new Vector3d(-2.0, 0, 0);
        var stern = state.Boat.PositionWorldM + q.Rotate(sternBody - state.Mass!.CenterOfMassBodyM);
        var flow = state.Boat.LinearVelocityWorldMps - state.Input.Environment.Water.VelocityAt(stern, state.TimeSeconds);
        var speed = Math.Sqrt(flow.X * flow.X + flow.Y * flow.Y);
        if (speed < 0.25) return;
        lastTime = state.TimeSeconds;
        var side = new Vector3d(-flow.Y, flow.X, 0) / speed;
        if (samples.Count == Capacity) samples.Dequeue();
        samples.Enqueue(new(stern, side, state.TimeSeconds, Math.Clamp(speed / 2.5, 0, 1)));
    }
}

public readonly record struct CenterOfMassSample(Vector3d PositionWorldM, double TimeSeconds);

/// <summary>
/// Bounded, simulation-time history of the world center of mass (the rigid-body position).
/// Records while hidden so enabling the trace shows recent history immediately.
/// </summary>
public sealed class CenterOfMassTrail
{
    public const double IntervalSeconds = 0.05;
    public const double LifetimeSeconds = 60;
    public const int Capacity = 1201;
    private readonly Queue<CenterOfMassSample> samples = new(Capacity);
    private double lastTime = double.NegativeInfinity;
    public IReadOnlyCollection<CenterOfMassSample> Samples => samples;

    public void Clear()
    {
        samples.Clear();
        lastTime = double.NegativeInfinity;
    }

    public void Update(SimulationState state)
    {
        // Time running backwards means a new simulation; stale history would draw a false jump.
        if (state.TimeSeconds < lastTime) Clear();
        while (samples.TryPeek(out var first) && state.TimeSeconds - first.TimeSeconds > LifetimeSeconds) samples.Dequeue();
        if (state.TimeSeconds - lastTime < IntervalSeconds - 1e-9) return; // Tolerate step-time rounding.
        lastTime = state.TimeSeconds;
        if (samples.Count == Capacity) samples.Dequeue();
        samples.Enqueue(new(state.Boat.PositionWorldM, state.TimeSeconds));
    }
}
