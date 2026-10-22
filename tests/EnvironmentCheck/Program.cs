using SailingSimulator.Adapters;
using SailingSimulator.Core;
using static Check;

var model = new IlcaBoatModel();
var masses = new IlcaMassModel();
var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6, outhaul: 0.5, cunningham: 0.3, hiking: -0.3);
var tests = new (string Name, Action Run)[]
{
    ("Wind units, compass quadrants, limits, and apparent wind at moving sections", () =>
    {
        var defaults = PresentationConversions.EnvironmentFromSettings();
        Near(defaults.TrueWindVelocityWorldMps, new(-1852.0 / 360, 0, 0), 1e-14);
        Require(defaults.WavePreset == WavePreset.Gentle && defaults.WaveDirectionMode == WaveDirectionMode.AlignedWithWind, "Default settings changed.");
        foreach (var (angle, travel) in new[] { (0, -Vector3d.UnitX), (90, -Vector3d.UnitY), (180, Vector3d.UnitX), (270, Vector3d.UnitY), (360, -Vector3d.UnitX) })
        {
            var config = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 30, windFromDegrees: angle, waveSeed: 1);
            Near(config.TrueWindVelocityWorldMps, travel * (30 * 1852.0 / 3600), 1e-13);
            Near(config.Water.TravelDirectionWorld, travel, 1e-14);
        }
        var input = Input(WavePreset.Gentle);
        var q = Quaterniond.FromAxisAngle(Vector3d.UnitZ, 0.8) * Quaterniond.FromAxisAngle(Vector3d.UnitX, 0.1);
        var boat = model.CreateInitialState(input) with { OrientationBodyToWorld = q, LinearVelocityWorldMps = new(1, 2, 0.1), AngularVelocityBodyRadPerSec = new(0.03, -0.02, 0.08) };
        var mass = masses.Evaluate(controls);
        var e = new ForceEvaluation(2, boat, mass, input, new(0.5, 0));
        foreach (var section in model.Sail.Shape(e).Sections)
        {
            var localVelocity = q.Conjugate().Rotate(boat.LinearVelocityWorldMps) + Vector3d.Cross(boat.AngularVelocityBodyRadPerSec, section.CenterBodyM - mass.CenterOfMassBodyM);
            Near(section.ApparentWindBodyMps, q.Conjugate().Rotate(input.Environment.TrueWindVelocityWorldMps) - localVelocity, 1e-13);
        }
        Throws<ArgumentOutOfRangeException>(() => PresentationConversions.EnvironmentFromSettings(windSpeedKnots: -0.01));
        Throws<ArgumentOutOfRangeException>(() => PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 30.01));
        Throws<ArgumentOutOfRangeException>(() => PresentationConversions.EnvironmentFromSettings(windFromDegrees: 361));
    }),
    ("Seed replay, varied sessions, immutable realization and sampling order", () =>
    {
        foreach (var seed in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            var a = Input(WavePreset.Gentle, seed).Environment;
            var b = Input(WavePreset.Gentle, seed).Environment;
            Require(a == b && a.GetHashCode() == b.GetHashCode(), "Configuration value equality lost.");
            Require(a.Water.Components.SequenceEqual(b.Water.Components), "Equal seeds differ.");
            var first = a.Water.SampleSurface(new(1, -2, 0), 3.7);
            Parallel.For(0, 100, i => a.Water.SampleSurface(new(i, i * 0.5, 0), i * 0.17));
            Require(first == a.Water.SampleSurface(new(1, -2, 0), 3.7) && first == b.Water.SampleSurface(new(1, -2, 0), 3.7), "Sampling changed realization.");
            Throws<NotSupportedException>(() => ((IList<WaveComponent>)a.Water.Components)[0] = default);
        }
        Require(!Input(WavePreset.Gentle, 1).Environment.Water.Components.SequenceEqual(Input(WavePreset.Gentle, 2).Environment.Water.Components), "Seed ignored.");
        var varied = Enumerable.Range(0, 8).Select(_ => PresentationConversions.EnvironmentFromSettings().WaveSeed).Distinct().Count();
        Require(varied > 1, "Sessions should choose a seed when omitted.");
    }),
    ("Spectrum strength, wavelength and directional spreading", () =>
    {
        foreach (var preset in Enum.GetValues<WavePreset>())
        {
            var field = Input(preset).Environment.Water;
            var hs = 4 * Math.Sqrt(field.Components.Sum(c => c.AmplitudeM * c.AmplitudeM) / 2);
            Near(hs, preset == WavePreset.Calm ? 0 : preset == WavePreset.Gentle ? 0.12 : 0.30, 1e-15);
            Require(field.Components.All(c => c.WavelengthM >= 3 && c.WavelengthM <= 12), "Wavelength outside documented range.");
            Require(field.Components.All(c => Vector3d.Dot(c.TravelDirectionWorld, field.TravelDirectionWorld) >= Math.Cos(25 * Math.PI / 180) - 1e-14), "Spreading exceeds 25 degrees.");
        }
        var gentle = Input(WavePreset.Gentle).Environment.Water;
        var moderate = Input(WavePreset.Moderate).Environment.Water;
        for (var i = 0; i < 100; i++)
        {
            var p = new Vector3d(i * 0.17, -i * 0.31, 0.2);
            Near(moderate.SampleSurface(p, i * 0.13).ElevationM, 2.5 * gentle.SampleSurface(p, i * 0.13).ElevationM, 1e-14);
            Near(moderate.VelocityAt(p, i * 0.13), 2.5 * gentle.VelocityAt(p, i * 0.13), 1e-14);
        }
    }),
    ("Surface normal and vertical orbital flow obey height derivatives", () =>
    {
        var water = Input(WavePreset.Gentle).Environment.Water;
        const double dt = 1e-5;
        for (var i = 0; i < 20; i++)
        {
            var p = new Vector3d(i * 0.21, i * -0.32, 0);
            var t = i * 0.41;
            var s = water.SampleSurface(p, t);
            var dx = (water.SampleSurface(p + Vector3d.UnitX * dt, t).ElevationM - water.SampleSurface(p - Vector3d.UnitX * dt, t).ElevationM) / (2 * dt);
            var dy = (water.SampleSurface(p + Vector3d.UnitY * dt, t).ElevationM - water.SampleSurface(p - Vector3d.UnitY * dt, t).ElevationM) / (2 * dt);
            var normal = new Vector3d(-dx, -dy, -1);
            Near(s.NormalWorld, normal / normal.Length, 1e-9);
            var dzdt = (water.SampleSurface(p, t + dt).PositionWorldZM - water.SampleSurface(p, t - dt).PositionWorldZM) / (2 * dt);
            Near(water.VelocityAt(p, t).Z, dzdt, 1e-9);
            Require(water.VelocityAt(p + Vector3d.UnitZ * 100, t).Length < 1e-20, "Deep orbital flow should decay.");
        }
        var calm = Input(WavePreset.Calm).Environment.Water;
        Require(calm.SampleSurface(new(10, -7, 5), 100) == new WaterSurfaceSample(0, -Vector3d.UnitZ), "Calm surface changed.");
        Near(calm.VelocityAt(new(1, 2, 3), 100), default, 0);
        Throws<ArgumentException>(() => water.SampleSurface(default, double.NaN));
        Throws<ArgumentException>(() => water.VelocityAt(new(double.PositiveInfinity, 0, 0), 0));
    }),
    ("Independent wave direction preserves wind and zero-wind alignment", () =>
    {
        var aligned = PresentationConversions.EnvironmentFromSettings(windFromDegrees: 37, waveSeed: 7);
        var independent = PresentationConversions.EnvironmentFromSettings(windFromDegrees: 37, waveDirectionMode: WaveDirectionMode.Independent, waveFromDegrees: 127, waveSeed: 7);
        Require(aligned.TrueWindVelocityWorldMps == independent.TrueWindVelocityWorldMps, "Wave direction mutated wind.");
        var zeroWind = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 0, windFromDegrees: 37, waveSeed: 7);
        Require(zeroWind.Water.Components.SequenceEqual(aligned.Water.Components), "Zero wind lost requested wave bearing.");
        var point = new Vector3d(1.2, 3.4, 0.2);
        var rotated = new Vector3d(-point.Y, point.X, point.Z);
        Near(independent.Water.SampleSurface(rotated, 2).ElevationM, aligned.Water.SampleSurface(point, 2).ElevationM, 1e-14);
        var v = aligned.Water.VelocityAt(point, 2);
        Near(independent.Water.VelocityAt(rotated, 2), new(-v.Y, v.X, v.Z), 1e-14);
    }),
    ("Local height changes buoyancy; orbital flow drives hull and separate foils", () =>
    {
        var calm = Input(WavePreset.Calm, wind: 0);
        var waves = Input(WavePreset.Gentle, wind: 0);
        var boat = model.CreateInitialState(calm);
        var flat = new ForceEvaluation(0, boat, masses.Evaluate(controls), calm);
        var moving = flat with { Input = waves };
        var a = model.Inspect(flat);
        var b = model.Inspect(moving);
        Require((a.Buoyancy.ForceWorldN - b.Buoyancy.ForceWorldN).Length > 1, "Wave elevation did not affect displacement.");
        Require(b.HullDrag.ForceWorldN.Length > 0.01 && b.Centerboard.ForceWorldN.Length > 0.01 && b.Rudder.ForceWorldN.Length > 0.01, "Orbital flow must reach hull, board and rudder.");
        Require(model.Inspect(moving with { TimeSeconds = 0.5 }).Buoyancy != b.Buoyancy, "Force evaluation ignored stage time.");
        var dry = moving with { Boat = boat with { PositionWorldM = new(0, 0, -5) } };
        var dryForces = model.Inspect(dry);
        Require(dryForces.Buoyancy == default && dryForces.HullDrag == default && dryForces.Centerboard == default && dryForces.Rudder == default, "Airborne boat has water force.");
        var raised = moving with { Input = new(new SailingControls(centerboard: 0), waves.Environment) };
        Require(model.Centerboard.Evaluate(raised) == default, "Raised board has water force.");
        // An upright fully submerged hull moving with the local flow at its centre still feels
        // spatially different water at its samples; uniform COM flow must not replace local flow.
        var p = boat.PositionWorldM;
        var matched = moving with { Boat = boat with { LinearVelocityWorldMps = waves.Environment.Water.VelocityAt(p, 0) } };
        Require(model.Hull.EvaluateHull(matched).Drag.ForceWorldN.Length > 0.01, "Hull used a single COM water sample.");
    }),
    ("Seeded waves reproduce all six degrees and survive environment replacement", () =>
    {
        var firstInput = Input(WavePreset.Gentle, wind: 0);
        var secondInput = Input(WavePreset.Gentle, wind: 0);
        var initial = model.CreateInitialState(Input(WavePreset.Calm, wind: 0));
        var first = new Simulation(initial, masses, model, firstInput);
        var second = new Simulation(initial, masses, new IlcaBoatModel(), secondInput);
        var calm = new Simulation(initial, masses, model, Input(WavePreset.Calm, wind: 0));
        var maximum = new double[6];
        var retained = first.Latest;
        var oldSurface = retained.Input.Environment.Water.SampleSurface(default, 1);
        for (var i = 0; i < 1800; i++)
        {
            if (i == 900)
            {
                firstInput = Input(WavePreset.Moderate, seed: -7, from: 125, wind: 0);
                secondInput = Input(WavePreset.Moderate, seed: -7, from: 125, wind: 0);
            }
            var a = first.Step(firstInput);
            var b = second.Step(secondInput);
            var c = calm.Step(calm.Latest.Input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass && a.Input == b.Input, "Wave replay differs.");
            Require(a.Diagnostics!.Total == b.Diagnostics!.Total && a.Diagnostics.SailShape.Sections.SequenceEqual(b.Diagnostics.SailShape.Sections), "Wave diagnostics differ.");
            var dv = a.Boat.LinearVelocityWorldMps - c.Boat.LinearVelocityWorldMps;
            var dw = a.Boat.AngularVelocityBodyRadPerSec - c.Boat.AngularVelocityBodyRadPerSec;
            var values = new[] { dv.X, dv.Y, dv.Z, dw.X, dw.Y, dw.Z };
            for (var axis = 0; axis < 6; axis++) maximum[axis] = Math.Max(maximum[axis], Math.Abs(values[axis]));
            if (i == 899)
                Require(maximum.All(v => v > 1e-4), $"Gentle waves missing a degree of response: {string.Join(", ", maximum)}");
            Bounded(a);
        }
        Require(maximum.All(v => v > 1e-4), $"Missing wave response: {string.Join(", ", maximum)}");
        Require(retained.StepIndex == 0 && retained.Input.Environment.Water.SampleSurface(default, 1) == oldSurface, "Environment replacement mutated old snapshot.");
        Console.WriteLine($"  Maximum wave/calm velocity differences: {string.Join(", ", maximum.Select(v => v.ToString("G4")))}");
    }),
    ("Gentle and moderate fixed-step runs remain bounded across winds and directions", () =>
    {
        foreach (var preset in new[] { WavePreset.Gentle, WavePreset.Moderate })
        foreach (var wind in new[] { 0.0, 10.0, 30.0 })
        {
            var input = Input(preset, seed: 42, from: wind == 10 ? 90 : 0, wind: wind);
            var sim = new Simulation(model.CreateInitialState(input), masses, model, input);
            for (var i = 0; i < 3600; i++) Bounded(sim.Step(input));
        }
    })
};

var selected = args.Length == 0 ? tests : tests.Where(t => t.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();
Require(selected.Length > 0, "No matching regression group.");
foreach (var (name, run) in selected) { run(); Console.WriteLine($"PASS {name}"); }
Console.WriteLine($"Phase 4 environment: {selected.Length} regression groups passed.");

SimulationInput Input(WavePreset preset, int seed = 1, double from = 270, double wind = 10) => new(controls,
    PresentationConversions.EnvironmentFromSettings(windSpeedKnots: wind, windFromDegrees: 270, wavePreset: preset,
        waveDirectionMode: WaveDirectionMode.Independent, waveFromDegrees: from, waveSeed: seed));

static class Check
{
    public static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Near(double actual, double expected, double tolerance) => Require(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"Expected {expected:R} +/- {tolerance:R}, got {actual:R}.");
    public static void Near(Vector3d actual, Vector3d expected, double tolerance) => Near((actual - expected).Length, 0, tolerance);
    public static void Throws<T>(Action run) where T : Exception
    { try { run(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    public static void Bounded(SimulationState state)
    {
        Require(state.Boat.PositionWorldM.Length < 10000 && state.Boat.LinearVelocityWorldMps.Length < 30 && state.Boat.AngularVelocityBodyRadPerSec.Length < 30, "Wave trajectory unbounded.");
        Near(state.Boat.OrientationBodyToWorld.LengthSquared, 1, 1e-12);
    }
}
