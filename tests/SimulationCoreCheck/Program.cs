using SailingSimulator.Adapters;
using SailingSimulator.Core;
using static Assertions;

var input = new SimulationInput(new SailingControls(), PresentationConversions.EnvironmentFromSettings());
var unitMass = new MassProperties(1, new(1, 1, 1));
var tests = new (string Name, Action Check)[]
{
    ("Coasting and fixed simulation clock", () =>
    {
        var initial = RigidBodyState.AtRest with { PositionWorldM = new(3, -2, 1), LinearVelocityWorldMps = new(2, 3, -4) };
        var sim = Make(initial);
        var original = sim.Latest;
        Step(sim, 600);
        Near(sim.Latest.Boat.PositionWorldM, new(23, 28, -39), 1e-11);
        Require(sim.Latest.StepIndex == 600 && sim.Latest.TimeSeconds == 10, "Fixed-step clock drifted.");
        Require(original.StepIndex == 0 && original.Boat == initial, "Published snapshot was mutated.");
    }),
    ("Constant world force and body torque", () =>
    {
        var mass = new MassProperties(2, new(2, 2, 2));
        var sim = new Simulation(RigidBodyState.AtRest, mass,
            new FunctionModel(_ => new(new(4, -2, 6), new(0, 0, 4))), input);
        Step(sim, 120);
        Near(sim.Latest.Boat.PositionWorldM, new(4, -2, 6), 1e-12);
        Near(sim.Latest.Boat.LinearVelocityWorldMps, new(4, -2, 6), 1e-12);
        Near(sim.Latest.Boat.AngularVelocityBodyRadPerSec, new(0, 0, 4), 1e-12);
        RotationNear(sim.Latest.Boat.OrientationBodyToWorld, Quaterniond.FromAxisAngle(Vector3d.UnitZ, 4), 1e-7);
    }),
    ("RK4 exponential velocity decay", () =>
    {
        var sim = Make(RigidBodyState.AtRest with { LinearVelocityWorldMps = Vector3d.UnitX },
            new FunctionModel(e => new(-e.Boat.LinearVelocityWorldMps, Vector3d.Zero)));
        Step(sim, 60);
        Near(sim.Latest.Boat.LinearVelocityWorldMps.X, Math.Exp(-1), 3e-10);
        Near(sim.Latest.Boat.PositionWorldM.X, 1 - Math.Exp(-1), 3e-10);
    }),
    ("RK4 stage times and frozen input", () =>
    {
        var evaluations = new List<ForceEvaluation>();
        var changed = new SimulationInput(new SailingControls(tiller: 0.7, hiking: -0.4),
            PresentationConversions.EnvironmentFromSettings(waveSeed: 99));
        var sim = Make(RigidBodyState.AtRest, new FunctionModel(e =>
        {
            evaluations.Add(e);
            return new(new(e.TimeSeconds, 0, 0), Vector3d.Zero);
        }));
        sim.Step(changed);
        Require(evaluations.Count == 4, "RK4 must evaluate exactly four stages.");
        var expectedTimes = new[] { 0, Simulation.StepSeconds / 2, Simulation.StepSeconds / 2, Simulation.StepSeconds };
        for (var i = 0; i < 4; i++)
        {
            Near(evaluations[i].TimeSeconds, expectedTimes[i], 1e-16);
            Require(ReferenceEquals(evaluations[i].Input, changed), "Step input changed between stages.");
            Near(evaluations[i].Boat.OrientationBodyToWorld.LengthSquared, 1, 1e-14);
        }
        Near(sim.Latest.Boat.LinearVelocityWorldMps.X, Math.Pow(Simulation.StepSeconds, 2) / 2, 1e-16);
        Near(sim.Latest.Boat.PositionWorldM.X, Math.Pow(Simulation.StepSeconds, 3) / 6, 1e-16);
        Require(ReferenceEquals(sim.Latest.Input, changed), "Snapshot must report the input actually sampled.");
    }),
    ("Long rotation remains normalized", () =>
    {
        var omega = new Vector3d(0.3, 0.5, 0.7);
        var sim = Make(RigidBodyState.AtRest with { AngularVelocityBodyRadPerSec = omega });
        Step(sim, 36_000);
        Near(sim.Latest.Boat.OrientationBodyToWorld.LengthSquared, 1, 1e-14);
        RotationNear(sim.Latest.Boat.OrientationBodyToWorld, Quaterniond.FromAxisAngle(omega, omega.Length * 600), 2e-8);
    }),
    ("Asymmetric inertia conserves torque-free momentum and energy", () =>
    {
        var inertia = new Vector3d(2, 3, 4);
        var omega = new Vector3d(0.1, 0.2, 0.3);
        var initialMomentum = new Vector3d(inertia.X * omega.X, inertia.Y * omega.Y, inertia.Z * omega.Z);
        var sim = new Simulation(RigidBodyState.AtRest with { AngularVelocityBodyRadPerSec = omega },
            new MassProperties(1, inertia), new CompositeForceModel(), input);
        Step(sim, 6000);
        var final = sim.Latest.Boat;
        var finalOmega = final.AngularVelocityBodyRadPerSec;
        var finalMomentum = new Vector3d(inertia.X * finalOmega.X, inertia.Y * finalOmega.Y, inertia.Z * finalOmega.Z);
        Near(final.OrientationBodyToWorld.Rotate(finalMomentum), initialMomentum, 1e-9);
        Near(Vector3d.Dot(finalOmega, finalMomentum), Vector3d.Dot(omega, initialMomentum), 1e-10);
    }),
    ("Deterministic replay with state and input dependent forces", () =>
    {
        var model = new FunctionModel(e => new(
            new(e.Input.Controls.Mainsheet + 0.1 * e.TimeSeconds, -0.2 * e.Boat.LinearVelocityWorldMps.Y, -0.1 * e.Boat.PositionWorldM.Z),
            new(0, 0, e.Input.Controls.Tiller - 0.3 * e.Boat.AngularVelocityBodyRadPerSec.Z)));
        var first = Make(RigidBodyState.AtRest, model);
        var second = Make(RigidBodyState.AtRest, model);
        for (var i = 0; i < 3000; i++)
        {
            var sampled = new SimulationInput(new SailingControls(tiller: Math.Sin(i * 0.01), mainsheet: (i % 100) / 100.0), input.Environment);
            Require(first.Step(sampled) == second.Step(sampled), $"Replay differs at step {i + 1}.");
        }
    }),
    ("Body/world rotation, force lever arms, and source composition", () =>
    {
        var q = Quaterniond.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2);
        Near(q.Rotate(Vector3d.UnitX), Vector3d.UnitY, 1e-14);
        Near(q.Conjugate().Rotate(q.Rotate(new(2, -3, 4))), new(2, -3, 4), 1e-14);
        var contribution = ForceMoment.AtBodyPoint(Vector3d.UnitY, Vector3d.UnitX, q);
        Near(contribution.ForceWorldN, -Vector3d.UnitX, 1e-14);
        Near(contribution.MomentBodyNm, Vector3d.UnitZ, 1e-14);
        IForceModel[] sources = { new FunctionModel(_ => contribution), new FunctionModel(_ => contribution) };
        var composite = new CompositeForceModel(sources);
        sources[0] = new FunctionModel(_ => default);
        var evaluation = new ForceEvaluation(0, RigidBodyState.AtRest, unitMass, input);
        var sum = composite.Evaluate(in evaluation);
        Require(sum == contribution + contribution, "Force source list was mutated or not summed.");
    }),
    ("Godot vector and quaternion basis conversion", () =>
    {
        Require(PresentationConversions.WorldToGodot(Vector3d.UnitX) == new GodotVector3d(0, 0, -1), "North must be Godot -Z.");
        Require(PresentationConversions.WorldToGodot(Vector3d.UnitY) == new GodotVector3d(1, 0, 0), "East must be Godot +X.");
        Require(PresentationConversions.WorldToGodot(Vector3d.UnitZ) == new GodotVector3d(0, -1, 0), "Down must be Godot -Y.");
        var vector = new Vector3d(2, -3, 4);
        var q = Quaterniond.FromAxisAngle(new(1, 2, 3), 1.234);
        Near(PresentationConversions.GodotToWorld(PresentationConversions.WorldToGodot(vector)), vector, 0);
        var converted = PresentationConversions.BodyToWorldToGodot(q);
        RotationNear(PresentationConversions.GodotToBodyToWorld(converted), q, 1e-14);
        var godotVector = PresentationConversions.WorldToGodot(vector);
        var godotRotated = new Quaterniond(converted.X, converted.Y, converted.Z, converted.W).Rotate(new(godotVector.X, godotVector.Y, godotVector.Z));
        var expected = PresentationConversions.WorldToGodot(q.Rotate(vector));
        Near(godotRotated, new(expected.X, expected.Y, expected.Z), 1e-14);
        var heading = PresentationConversions.GetAttitude(Quaterniond.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2));
        Near(heading.Heading, Math.PI / 2, 1e-14);
        Near(PresentationConversions.GetAttitude(Quaterniond.FromAxisAngle(Vector3d.UnitX, 0.2)).Heel, 0.2, 1e-14);
        Near(PresentationConversions.GetAttitude(Quaterniond.FromAxisAngle(Vector3d.UnitY, 0.3)).Pitch, 0.3, 1e-14);
    }),
    ("Compass from-directions, defaults, and independent waves", () =>
    {
        var defaults = PresentationConversions.EnvironmentFromSettings();
        Near(defaults.TrueWindVelocityWorldMps, new(-1852.0 / 360, 0, 0), 1e-14);
        Require(defaults.WavePreset == WavePreset.Gentle && defaults.WaveDirectionMode == WaveDirectionMode.AlignedWithWind, "Default wave settings changed.");
        Near(PresentationConversions.EnvironmentFromSettings(windFromDegrees: 90).WaveTravelDirectionWorld, -Vector3d.UnitY, 1e-14);
        Near(PresentationConversions.EnvironmentFromSettings(windFromDegrees: 180).WaveTravelDirectionWorld, Vector3d.UnitX, 1e-14);
        Near(PresentationConversions.EnvironmentFromSettings(windFromDegrees: 270).WaveTravelDirectionWorld, Vector3d.UnitY, 1e-14);
        Require(PresentationConversions.EnvironmentFromSettings(windFromDegrees: 360) == defaults, "360 and 0 degrees differ.");
        var independent = PresentationConversions.EnvironmentFromSettings(waveDirectionMode: WaveDirectionMode.Independent, waveFromDegrees: 90, waveSeed: -5);
        Near(independent.WaveTravelDirectionWorld, -Vector3d.UnitY, 1e-14);
        Require(independent.WaveSeed == -5, "Seed was not preserved.");
        var calm = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 0, windFromDegrees: 90);
        Near(calm.TrueWindVelocityWorldMps, Vector3d.Zero, 0);
        Near(calm.WaveTravelDirectionWorld, -Vector3d.UnitY, 1e-14);
        Near(PresentationConversions.MetersPerSecondToKnots(PresentationConversions.KnotsToMetersPerSecond(30)), 30, 1e-14);
    }),
    ("Reject invalid input, mass, configuration, and initial state", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => new SailingControls(tiller: double.NaN));
        Throws<ArgumentOutOfRangeException>(() => new SailingControls(hiking: 1.01));
        Throws<ArgumentOutOfRangeException>(() => new SailingControls(mainsheet: -0.1));
        Throws<ArgumentOutOfRangeException>(() => new SailingControls(centerboard: double.PositiveInfinity));
        Throws<ArgumentOutOfRangeException>(() => new MassProperties(0, Vector3d.UnitX));
        Throws<ArgumentOutOfRangeException>(() => new MassProperties(1, new(1, -1, 1)));
        Throws<ArgumentException>(() => Make(RigidBodyState.AtRest with { OrientationBodyToWorld = default }));
        Throws<ArgumentException>(() => Make(RigidBodyState.AtRest with { PositionWorldM = new(double.NaN, 0, 0) }));
        Throws<ArgumentOutOfRangeException>(() => PresentationConversions.EnvironmentFromSettings(windSpeedKnots: 31));
        Throws<ArgumentOutOfRangeException>(() => PresentationConversions.EnvironmentFromSettings(windFromDegrees: double.NaN));
        Throws<ArgumentException>(() => PresentationConversions.EnvironmentFromSettings(waveDirectionMode: WaveDirectionMode.Independent));
        Throws<ArgumentException>(() => PresentationConversions.EnvironmentFromSettings(waveFromDegrees: 90));
        Throws<ArgumentException>(() => new EnvironmentConfig(new(0, 0, 1), WavePreset.Gentle, WaveDirectionMode.AlignedWithWind, Vector3d.UnitX, 1));
        Throws<ArgumentException>(() => new EnvironmentConfig(new(1e308, 1e308, 0), WavePreset.Gentle, WaveDirectionMode.Independent, Vector3d.UnitX, 1));
        Throws<ArgumentException>(() => new EnvironmentConfig(Vector3d.UnitX, WavePreset.Gentle, WaveDirectionMode.AlignedWithWind, Vector3d.UnitY, 1));
        Throws<ArgumentException>(() => new EnvironmentConfig(Vector3d.Zero, (WavePreset)999, WaveDirectionMode.Independent, Vector3d.UnitX, 1));
        var normalized = Make(RigidBodyState.AtRest with { OrientationBodyToWorld = new(0, 0, 0, 2) });
        Require(normalized.Latest.Boat.OrientationBodyToWorld == Quaterniond.Identity, "Initialization must normalize orientation.");
    }),
    ("Non-finite RK4 stage fails atomically and reports stage", () =>
    {
        var sim = Make(RigidBodyState.AtRest, new FunctionModel(e => e.TimeSeconds > 0
            ? new(new(double.NaN, 0, 0), Vector3d.Zero) : default));
        var before = sim.Latest;
        var exception = Throws<SimulationStepException>(() => sim.Step(input));
        Require(exception.AttemptedStepIndex == 1 && exception.Stage == "k2", "Failure must identify the attempted step and RK4 stage.");
        Require(ReferenceEquals(before, sim.Latest), "A failed step published partial state.");
        var overflow = new Simulation(RigidBodyState.AtRest, new MassProperties(1e-308, new(1, 1, 1)),
            new FunctionModel(_ => new(new(1e308, 0, 0), Vector3d.Zero)), input);
        Require(Throws<SimulationStepException>(() => overflow.Step(input)).Stage == "k1", "Overflow must surface immediately.");
        Require(overflow.Latest.StepIndex == 0, "Overflow advanced the clock.");
    }),
    ("Concurrent input exchange preserves complete immutable snapshots", () =>
    {
        var first = new SimulationInput(new SailingControls(tiller: -1), PresentationConversions.EnvironmentFromSettings(waveSeed: -1));
        var second = new SimulationInput(new SailingControls(tiller: 1), PresentationConversions.EnvironmentFromSettings(waveSeed: 1));
        var mailbox = new SnapshotMailbox<SimulationInput>(first);
        using var barrier = new Barrier(2);
        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 100_000; i++) mailbox.Publish(i % 2 == 0 ? first : second);
        });
        var reader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 100_000; i++)
            {
                var sampled = mailbox.Read();
                Require(sampled.Controls.Tiller == sampled.Environment.WaveSeed, "Input snapshot was torn.");
            }
        });
        Task.WhenAll(writer, reader).GetAwaiter().GetResult();
    }),
    ("Concurrent rendering reads completed states", () =>
    {
        var sim = Make(RigidBodyState.AtRest with { LinearVelocityWorldMps = Vector3d.UnitX });
        using var barrier = new Barrier(2);
        var writer = Task.Run(() => { barrier.SignalAndWait(); Step(sim, 10_000); });
        var reader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            long previous = 0;
            for (var i = 0; i < 100_000; i++)
            {
                var snapshot = sim.Latest;
                Require(snapshot.StepIndex >= previous, "Published time moved backwards.");
                Require(snapshot.TimeSeconds == snapshot.StepIndex * Simulation.StepSeconds, "Snapshot time/index was torn.");
                Near(snapshot.Boat.PositionWorldM.X, snapshot.TimeSeconds, 1e-9);
                previous = snapshot.StepIndex;
            }
        });
        Task.WhenAll(writer, reader).GetAwaiter().GetResult();
    })
};

foreach (var test in tests)
{
    test.Check();
    Console.WriteLine($"PASS {test.Name}");
}
Console.WriteLine($"Phase 2 simulation core: {tests.Length} regression checks passed.");

Simulation Make(RigidBodyState initial, IForceModel? model = null) => new(initial, unitMass, model ?? new CompositeForceModel(), input);
void Step(Simulation sim, int count) { for (var i = 0; i < count; i++) sim.Step(input); }

static class Assertions
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Near(double actual, double expected, double tolerance)
    {
        Require(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"Expected {expected:R} +/- {tolerance:R}; got {actual:R}.");
    }

    public static void Near(Vector3d actual, Vector3d expected, double tolerance) => Near((actual - expected).Length, 0, tolerance);

    public static void RotationNear(Quaterniond actual, Quaterniond expected, double tolerance)
    {
        Near(actual.Rotate(Vector3d.UnitX), expected.Rotate(Vector3d.UnitX), tolerance);
        Near(actual.Rotate(Vector3d.UnitY), expected.Rotate(Vector3d.UnitY), tolerance);
        Near(actual.Rotate(Vector3d.UnitZ), expected.Rotate(Vector3d.UnitZ), tolerance);
    }

    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}

sealed class FunctionModel(Func<ForceEvaluation, ForceMoment> evaluate) : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation evaluation) => evaluate(evaluation);
}
