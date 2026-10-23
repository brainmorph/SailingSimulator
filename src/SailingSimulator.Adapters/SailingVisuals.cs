using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

/// <summary>Presentation-only deformation of the published aerodynamic strips, in hull-datum NED.
/// No control-to-shape calculation or force evaluation occurs here.</summary>
public static class SailingVisuals
{
    public static Vector3d SailPoint(SailSectionState section, double chordFraction, double timeSeconds)
    {
        var chord = new Vector3d(-Math.Cos(section.AngleRad), Math.Sin(section.AngleRad), 0);
        var normal = new Vector3d(Math.Sin(section.AngleRad), Math.Cos(section.AngleRad), 0);
        var mast = section.CenterBodyM - chord * (0.4 * section.ChordM);
        var windAcross = Vector3d.Dot(section.ApparentWindBodyMps, normal);
        var side = windAcross < 0 ? -1 : 1;
        var speed = section.ApparentWindBodyMps.Length;
        var envelope = Math.Sin(Math.PI * chordFraction);
        var belly = side * section.CamberRatio * section.ChordM * 4 * chordFraction * (1 - chordFraction);
        // Luffing changes displacement, never the aerodynamic angle/force; no wind means no flutter.
        var flutter = section.Luffing * Math.Min(1, speed / 2) * section.ChordM * 0.11 * envelope *
            Math.Sin(timeSeconds * 24 + section.HeightM * 5 + chordFraction * 13);
        return mast + chord * (section.ChordM * chordFraction) + normal * (belly + flutter);
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

    public static double Separation(SailSectionState section)
    {
        var wind = section.ApparentWindBodyMps;
        var speed = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
        if (speed < 0.1) return 0;
        var across = wind.X * Math.Sin(section.AngleRad) + wind.Y * Math.Cos(section.AngleRad);
        // A visible high-incidence cue, not a measured boundary-layer model.
        return Math.Clamp((Math.Abs(across) / speed - 0.35) / 0.45, 0, 1);
    }

    /// <summary>Paired ribbons: attached flow streams along the sail; luff/high incidence breaks it up.</summary>
    public static Vector3d TelltalePoint(SailSectionState section, int side, double along, double timeSeconds)
    {
        var chord = new Vector3d(-Math.Cos(section.AngleRad), Math.Sin(section.AngleRad), 0);
        var normal = new Vector3d(Math.Sin(section.AngleRad), Math.Cos(section.AngleRad), 0);
        var wind = section.ApparentWindBodyMps;
        var speed = wind.Length;
        var anchor = SailPoint(section, 0.27, timeSeconds) + normal * (side * 0.018);
        if (speed < 0.1) return anchor + Vector3d.UnitZ * (along * 0.27);
        var flowAlong = Vector3d.Dot(wind, chord);
        var flowAcross = Vector3d.Dot(wind, normal);
        var leeward = flowAcross * side > 0;
        var disorder = Math.Max(section.Luffing, Separation(section) * (leeward ? 1 : 0.35));
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
        var stern = state.Boat.PositionWorldM + q.Rotate(new Vector3d(-2.0, 0, 0) - state.Mass!.CenterOfMassBodyM);
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
