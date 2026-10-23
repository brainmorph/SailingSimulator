using System.Diagnostics;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using static Check;

var tests = new (string Name, Action Run)[]
{
    ("Fixed-step held inputs, rates, bounds and release", () =>
    {
        var all = new HeldSailingInput(1, 1, 1, 1, 1, 1, 1, 1);
        var controls = new SailingControls(centerboard: 0);
        for (var i = 0; i < 60; i++) controls = all.Advance(controls);
        Near(controls.Tiller, 1);
        Near(controls.Mainsheet, 0.25);
        Near(controls.Traveler, 0.25);
        Near(controls.Vang, 0.25);
        Near(controls.Outhaul, 0.25);
        Near(controls.Cunningham, 0.25);
        Near(controls.Centerboard, 0.25);
        Near(controls.Hiking, 0.5);
        for (var i = 0; i < 600; i++) controls = all.Advance(controls);
        Require(controls == new SailingControls(1, 1, 1, 1, 1, 1, 1, 1), "Upper bounds");
        for (var i = 0; i < 60; i++) controls = HeldSailingInput.Released.Advance(controls);
        Require(controls == new SailingControls(0, 1, 1, 1, 1, 1, 1, 1), "Release must centre only tiller");
        var negative = new HeldSailingInput(-1, -1, -1, -1, -1, -1, -1, -1);
        for (var i = 0; i < 600; i++) controls = negative.Advance(controls);
        Require(controls == new SailingControls(-1, 0, 0, 0, 0, 0, 0, -1), "Lower bounds");
        Require(ReferenceEquals(controls, negative.Advance(controls)), "Unchanged controls reuse snapshot");
        Throws<ArgumentOutOfRangeException>(() => new HeldSailingInput(tiller: double.NaN));
        Throws<ArgumentOutOfRangeException>(() => new HeldSailingInput(hiking: 1.1));
        Throws<ArgumentOutOfRangeException>(() => all.Advance(controls, -1));
    }),
    ("Absolute deadlines retain misses without catch-up or dropped time", () =>
    {
        var schedule = new FixedStepSchedule(100, 60_000);
        Require(schedule.NextStart == 100 && schedule.BeginStep(100) == 1100, "Initial deadline");
        Require(schedule.NextStart == 1100, "Second start");
        Require(schedule.BeginStep(1600) == 2100, "Deadline must remain anchored after delayed start");
        Require(schedule.NextStart == 2600, "Never shorten next interval to catch up");
        Throws<ArgumentOutOfRangeException>(() => schedule.BeginStep(2100));
        Require(schedule.BeginStep(4100) == 3100, "Overload must not reset or skip deadlines");
        Require(schedule.NextStart == 5100, "No burst after overload");
        var exact = new FixedStepSchedule(0, 10_000_000);
        long deadline = 0;
        for (var i = 0; i < 600; i++) deadline = exact.BeginStep(exact.NextStart);
        Require(deadline == 100_000_000, "Fractional frequency must not drift absolute deadlines");
    }),
    ("Concurrent host publication, step sampling and valid render reads", () =>
    {
        var positive = new HostInput(new(1, 1, 1, 1, 1, 1, 1, 1), Environment(1));
        var negative = new HostInput(new(-1, -1, -1, -1, -1, -1, -1, -1), Environment(-1));
        var initial = new SailingControls(mainsheet: 0.5, traveler: 0.5, vang: 0.5,
            outhaul: 0.5, cunningham: 0.5, centerboard: 0.5);
        var model = new AuditModel(initial, System.Environment.CurrentManagedThreadId);
        using var runner = Make(model, initial, positive.Environment);
        runner.PublishInput(positive);
        runner.Start();
        using var cancel = new CancellationTokenSource();
        var publisher = Task.Run(() =>
        {
            while (!cancel.IsCancellationRequested)
            {
                runner.PublishInput(positive);
                runner.PublishInput(negative);
                Thread.Sleep(1);
            }
        });
        long previous = 0, reads = 0;
        var until = Stopwatch.StartNew();
        while (runner.Latest.State.StepIndex < 90 && until.Elapsed.TotalSeconds < 5)
        {
            var frame = runner.Latest;
            Require(frame.Fault is null, frame.Fault?.Message ?? "Worker fault");
            Require(frame.State.StepIndex >= previous, "Snapshots went backwards");
            Require(frame.State.StepIndex == frame.Timing.CompletedSteps, "Mixed state and timing");
            Near(frame.State.TimeSeconds, frame.State.StepIndex * Simulation.StepSeconds);
            Require(frame.State.Boat.PositionWorldM.IsFinite, "Partial state");
            previous = frame.State.StepIndex;
            reads++;
            Thread.Sleep(1);
        }
        var retained = runner.Latest;
        cancel.Cancel();
        publisher.GetAwaiter().GetResult();
        runner.Stop();
        Require(previous >= 89 && reads > 50, "Insufficient concurrent coverage");
        Require(model.AuditedSteps >= 90 && model.WorkerThreadId != System.Environment.CurrentManagedThreadId, "Dedicated worker");
        Require(retained.State.StepIndex == retained.Timing.CompletedSteps, "Retained snapshot changed");
        Require(!runner.Latest.Running && runner.Latest.Fault is null, "Clean stop");
    }),
    ("Fault preserves last completed state and lifecycle is orderly", () =>
    {
        using var runner = Make(new FaultModel());
        runner.Start();
        WaitFor(() => runner.Latest.Fault is not null);
        runner.Stop();
        Require(!runner.Latest.Running && runner.Latest.State.StepIndex == 0, "Fault published incomplete step");
        Require(runner.Latest.Fault is SimulationStepException, "Failure lost stage context");
        runner.Stop();
        Throws<InvalidOperationException>(runner.Start);
        runner.Dispose();
        runner.Dispose();
        Throws<ObjectDisposedException>(runner.Start);
        using var unstarted = Make(new CompositeForceModel());
        unstarted.Stop();
        Throws<InvalidOperationException>(unstarted.Start);
        for (var i = 0; i < 5; i++)
        {
            using var session = Make(new CompositeForceModel());
            session.Start();
            WaitFor(() => session.Latest.State.StepIndex > 0);
            Task.WaitAll(Task.Run(session.Stop), Task.Run(session.Stop));
            var stopped = session.Latest;
            Thread.Sleep(20);
            Require(ReferenceEquals(stopped, session.Latest), "Worker published after joined shutdown");
        }
    }),
    ("Overload remains visible and triggers consecutive deadline warnings", () =>
    {
        Require(new StepTiming(0, 0, 0, 0, 2, 2).Warning == DeadlineWarning.None, "Early warning");
        Require(new StepTiming(0, 0, 0, 0, 3, 3).Warning == DeadlineWarning.Soft, "Soft threshold");
        Require(new StepTiming(0, 0, 0, 0, 5, 5).Warning == DeadlineWarning.Hard, "Hard threshold");
        using var runner = Make(new SlowModel());
        runner.Start();
        WaitFor(() => runner.Latest.State.StepIndex >= 8);
        runner.Stop();
        var snapshot = runner.Latest;
        Require(snapshot.Timing.Warning == DeadlineWarning.Hard && snapshot.Timing.TotalLateSteps >= 8, "Overload concealed");
        Require(snapshot.Timing.ElapsedSeconds - snapshot.State.TimeSeconds > 0.06, "Clock dropped simulation time");
        Require(snapshot.Timing.DeadlineLatenessMilliseconds > 60, "Deadlines reset after overload");
    }),
    ("Rolling performance percentiles, allocation rates and eviction", () =>
    {
        var window = new StepPerformanceWindow();
        PerformanceSnapshot? last = null;
        for (var i = 1; i <= 300; i++) last = window.Add(i, 100, 0.02);
        Near(last!.P50Ms, 150); Near(last.P95Ms, 285); Near(last.P99Ms, 297); Near(last.MaxMs, 300);
        Require(last.Samples == 300 && last.AllocatedBytes == 100, "Window count and current allocation");
        Near(last.AllocationBytesPerSecond, 5000);
        var retained = last;
        for (var i = 0; i < 300; i++) last = window.Add(1, 20, 0.01, 1, 2, 3, 4096);
        Near(last.P50Ms, 1); Near(last.P99Ms, 1); Near(last.MaxMs, 1);
        Require(Math.Abs(last.AllocationBytesPerSecond - 2000) < 1e-7, "Evict old allocations and elapsed time");
        Require(last.Gen0Collections == 1 && last.Gen1Collections == 2 && last.Gen2Collections == 3 &&
            last.ManagedHeapBytes == 4096 && retained.MaxMs == 300, "Immutable GC/heap observations");
        Throws<ArgumentOutOfRangeException>(() => window.Add(double.NaN, 1, 1));
        Throws<ArgumentOutOfRangeException>(() => window.Add(1, -1, 1));
        Throws<ArgumentOutOfRangeException>(() => window.Add(1, 0, 0));
    }),
    ("Profiling observes subsystems without changing physical trajectories", () =>
    {
        var input = new SimulationInput(new(mainsheet: 0.6, hiking: -0.3), Environment(123));
        var model = new IlcaBoatModel();
        var initial = model.CreateInitialState(input, Math.PI / 2);
        var measured = new Simulation(initial, new IlcaMassModel(), model, input);
        var plain = new Simulation(initial, new IlcaMassModel(), model, input);
        var profiler = new StepProfiler();
        double mass = 0, rk4 = 0, diagnostics = 0, hull = 0, centerboard = 0, rudder = 0, sail = 0;
        for (var i = 0; i < 120; i++)
        {
            if (i == 60) input = new(new(mainsheet: 0.5, hiking: -0.5, tiller: 0.1), Environment(321));
            profiler.Reset();
            var a = measured.Step(input, profiler);
            var b = plain.Step(input);
            Require(a.Boat == b.Boat && a.Rig == b.Rig && a.Mass == b.Mass &&
                a.Diagnostics!.Total == b.Diagnostics!.Total &&
                a.Diagnostics.SailShape.Sections.SequenceEqual(b.Diagnostics.SailShape.Sections), "Profiling changed physics");
            var p = profiler.Snapshot();
            Require(p.HullMs + p.CenterboardMs + p.RudderMs + p.SailMs <= p.Rk4Ms + p.DiagnosticsMs,
                "Nested subsystem accounting");
            // Windows Stopwatch ticks at 10 MHz, so a single fast scope can read zero; sum across steps.
            mass += p.MassMs; rk4 += p.Rk4Ms; diagnostics += p.DiagnosticsMs; hull += p.HullMs;
            centerboard += p.CenterboardMs; rudder += p.RudderMs; sail += p.SailMs;
        }
        Require(mass > 0 && rk4 > 0 && diagnostics > 0 && hull > 0 &&
            centerboard > 0 && rudder > 0 && sail > 0, "Missing subsystem timing");
        profiler.Reset();
        Require(profiler.Snapshot() == default, "Profile carries over between steps");
    }),
    ("Worker publishes allocation and process GC observations with completed states", () =>
    {
        using var runner = Make(new AllocatingModel());
        runner.Start();
        WaitFor(() => runner.Latest.State.StepIndex >= 10);
        runner.Stop();
        var s = runner.Latest;
        var p = s.Performance!;
        Require(p is not null && p.Samples == s.State.StepIndex, "Performance/state mismatch");
        Require(p!.AllocatedBytes >= 4 * 4096 && p.AllocationBytesPerSecond > 0, "Worker allocations missing");
        Require(p.ManagedHeapBytes > 0 && p.Gen0Collections >= 0 && p.Gen1Collections >= 0 && p.Gen2Collections >= 0,
            "GC/heap observations missing");
        Require(p.P50Ms >= 0 && p.P50Ms <= p.P95Ms && p.P95Ms <= p.P99Ms && p.P99Ms <= p.MaxMs,
            "Percentiles not ordered");
    }),
    ("Debug settings preserve seeds, zero-wind bearings and independent waves", () =>
    {
        var defaults = new DebugEnvironmentSettings(WaveSeed: -123);
        var e = defaults.Build();
        Near(MetersPerSecondToKnots(e.TrueWindVelocityWorldMps.Length), 10);
        Require(e.WavePreset == WavePreset.Gentle && e.WaveDirectionMode == WaveDirectionMode.AlignedWithWind, "Defaults");
        foreach (var bearing in new[] { 0.0, 90, 180, 270, 360 })
        {
            var settings = defaults with { WindSpeedKnots = 0, WindFromDegrees = bearing };
            var calmWind = settings.Build();
            Near(DebugEnvironmentSettings.FromBearing(calmWind.WaveTravelDirectionWorld), bearing % 360);
            var independent = (settings with { WindSpeedKnots = 30, AlignWaves = false, WaveFromDegrees = 45 }).Build();
            Near(DebugEnvironmentSettings.FromBearing(independent.WaveTravelDirectionWorld), 45);
            Require(independent.WaveSeed == -123 && calmWind.WaveSeed == -123, "Edit reshuffled seed");
        }
        foreach (var seed in new[] { int.MinValue, 0, int.MaxValue })
        {
            var settings = defaults with { WaveSeed = seed, AlignWaves = false, WindFromDegrees = 90, WaveFromDegrees = 270 };
            var a = settings.Build();
            var b = DebugEnvironmentSettings.FromEnvironment(a).Build();
            Require(a.WaveSeed == seed && a.TrueWindVelocityWorldMps == b.TrueWindVelocityWorldMps &&
                a.Water.SampleSurface(new(3, 4, 0), 5) == b.Water.SampleSurface(new(3, 4, 0), 5), "Condition round trip");
        }
        Throws<ArgumentOutOfRangeException>(() => (defaults with { WindSpeedKnots = 30.1 }).Build());
        Throws<ArgumentOutOfRangeException>(() => (defaults with { WindFromDegrees = -1 }).Build());
        Throws<ArgumentOutOfRangeException>(() => (defaults with { AlignWaves = false, WaveFromDegrees = 361 }).Build());
    }),
    ("Visual sail uses aerodynamic section geometry, trim and luffing", () =>
    {
        var model = new SectionalSailModel();
        var mass = new IlcaMassModel().Evaluate(new());
        ForceEvaluation Evaluation(SailingControls controls, Vector3d wind, double boom = 0.5) =>
            new(0, RigidBodyState.AtRest, mass,
                new(controls, new(wind, WavePreset.Calm, WaveDirectionMode.Independent, Vector3d.UnitX, 1)), new(boom, 0));
        var shape = model.Shape(Evaluation(new(), new(0, 5, 0)));
        var flat = model.Shape(Evaluation(new(outhaul: 1, cunningham: 1, vang: 1), new(0, 5, 0)));
        foreach (var section in shape.Sections)
        {
            var chord = new Vector3d(-Math.Cos(section.AngleRad), Math.Sin(section.AngleRad), 0);
            var mast = section.CenterBodyM - chord * (0.4 * section.ChordM);
            Require((SailingVisuals.SailPoint(section, 0, 0) - mast).Length < 1e-12, "Luff must stay on mast");
            Require((SailingVisuals.SailPoint(section, 1, 0) - mast - chord * section.ChordM).Length < 1e-12, "Leech must follow aerodynamic angle/chord");
        }
        Require((SailingVisuals.SailPoint(shape.Sections[0], 0.5, 0) - SailingVisuals.SailPoint(flat.Sections[0], 0.5, 0)).Length > 0.01,
            "Outhaul/cunningham camber must visibly change");
        Require((SailingVisuals.SailPoint(shape.Sections[^1], 1, 0) - SailingVisuals.SailPoint(flat.Sections[^1], 1, 0)).Length > 0.01,
            "Vang twist must visibly change");
        var foot = SailingVisuals.EdgeSection(shape, false);
        var head = SailingVisuals.EdgeSection(shape, true);
        Near(foot.HeightM, 0); Near(head.HeightM, SectionalSailModel.HeightM);
        Near(foot.ChordM, 2 * SectionalSailModel.AreaM2 / SectionalSailModel.HeightM); Near(head.ChordM, 0);
        var luff = model.Shape(Evaluation(new(), new(-5, 0, 0), 0));
        Require(luff.Sections[0].Luffing > 0.99, "Luff fixture");
        Require((SailingVisuals.SailPoint(luff.Sections[0], 0.4, 0) - SailingVisuals.SailPoint(luff.Sections[0], 0.4, 0.07)).Length > 0.03,
            "Luff must visibly flutter");
        Require(luff.Sections.Sum(x => x.Force.ForceWorldN.Length) < shape.Sections.Sum(x => x.Force.ForceWorldN.Length) * 0.01,
            "Visible luff must share reduced aerodynamic force");
        var noWind = luff.Sections[0] with { ApparentWindBodyMps = default };
        Require(SailingVisuals.SailPoint(noWind, 0.4, 0) == SailingVisuals.SailPoint(noWind, 0.4, 1), "No wind cannot animate sail");
    }),
    ("Telltales stream, reverse, droop and break up with local flow", () =>
    {
        var section = new SailSectionState(2, 1, 1.5, 0, 0.08, 0, 0,
            new(0.05, 0, -2.5), new(-5, 0, 0), default);
        foreach (var side in new[] { -1, 1 })
        {
            var root = SailingVisuals.TelltalePoint(section, side, 0, 0);
            var tip = SailingVisuals.TelltalePoint(section, side, 1, 0);
            Require(tip.X < root.X - 0.25, "Attached local flow must stream aft");
            var reversed = section with { ApparentWindBodyMps = new(5, 0, 0) };
            Require(SailingVisuals.TelltalePoint(reversed, side, 1, 0).X > SailingVisuals.TelltalePoint(reversed, side, 0, 0).X + 0.25,
                "Reverse flow must reverse the ribbon");
            var stopped = section with { ApparentWindBodyMps = default };
            var hanging = SailingVisuals.TelltalePoint(stopped, side, 1, 0);
            Require(hanging.Z > SailingVisuals.TelltalePoint(stopped, side, 0, 0).Z + 0.25, "Still-air ribbon must droop");
            Require(hanging == SailingVisuals.TelltalePoint(stopped, side, 1, 3), "Still air cannot flutter");
        }
        var separated = section with { ApparentWindBodyMps = new(0, 5, 0) };
        Require(SailingVisuals.Separation(section) == 0 && SailingVisuals.Separation(separated) == 1, "Local incidence separation cue");
        Require((SailingVisuals.TelltalePoint(separated, 1, 1, 0) - SailingVisuals.TelltalePoint(separated, 1, 1, 0.07)).Length > 0.02,
            "Separated telltale must visibly break up");
    }),
    ("Rendered spectral inputs match physical wave elevation and normals", () =>
    {
        foreach (var preset in Enum.GetValues<WavePreset>())
        foreach (var direction in new[] { 0.0, 75, 210 })
        {
            var environment = PresentationConversions.EnvironmentFromSettings(wavePreset: preset,
                waveDirectionMode: WaveDirectionMode.Independent, waveFromDegrees: direction, waveSeed: 123);
            foreach (var time in new[] { 0.0, 1.7, 19.0 })
            foreach (var position in new[] { new Vector3d(0, 0, 0), new Vector3d(7, -13, 0), new Vector3d(-83, 142, 0) })
            {
                var godot = PresentationConversions.WorldToGodot(position);
                double height = 0, slopeX = 0, slopeZ = 0;
                foreach (var component in environment.Water.Components)
                {
                    var m = SailingVisuals.WaterMode(component);
                    var phase = m.Wavenumber * (m.DirectionX * godot.X + m.DirectionZ * godot.Z) - m.Frequency * time + m.Phase;
                    height += m.AmplitudeM * Math.Cos(phase);
                    slopeX += m.DirectionX * m.AmplitudeM * m.Wavenumber * Math.Sin(phase);
                    slopeZ += m.DirectionZ * m.AmplitudeM * m.Wavenumber * Math.Sin(phase);
                }
                var water = environment.Water.SampleSurface(position, time);
                Near(height, water.ElevationM);
                var normal = PresentationConversions.WorldToGodot(water.NormalWorld);
                var length = Math.Sqrt(1 + slopeX * slopeX + slopeZ * slopeZ);
                Near(normal.X, slopeX / length); Near(normal.Y, 1 / length); Near(normal.Z, slopeZ / length);
            }
        }
    }),
    ("Wake follows water-relative motion with bounded simulation-time history", () =>
    {
        var trail = new WakeTrail();
        var input = new SimulationInput(new(), PresentationConversions.EnvironmentFromSettings(wavePreset: WavePreset.Calm, waveSeed: 123));
        var state = new SimulationState(0, 0, RigidBodyState.AtRest, input) { Mass = new IlcaMassModel().Evaluate(input.Controls) };
        trail.Update(state);
        Require(trail.Samples.Count == 0, "Stopped boat cannot create wake");
        for (var i = 1; i <= 600; i++)
        {
            var time = i / 60.0;
            trail.Update(state with { StepIndex = i, TimeSeconds = time, Boat = state.Boat with
            { PositionWorldM = new(time * 2, 0, 0), LinearVelocityWorldMps = new(2, 0, 0) } });
        }
        Require(trail.Samples.Count > 50 && trail.Samples.Count <= WakeTrail.Capacity, "Wake history bound");
        var count = trail.Samples.Count;
        trail.Update(state with { TimeSeconds = 10 });
        Require(trail.Samples.Count == count, "Repeated rendering cannot create extra wake");
        Require(trail.Samples.All(x => x.TimeSeconds >= 3 && x.SideWorld == Vector3d.UnitY), "Wake age or transverse direction");
        trail.Update(state with { TimeSeconds = 18 });
        Require(trail.Samples.Count == 0, "Stopped wake must decay completely");
    }),
    ("Ten-second ILCA wall-clock cadence without render reads", () =>
    {
        var input = new SimulationInput(new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, hiking: -0.3), Environment(123));
        var model = new IlcaBoatModel();
        using var runner = new SimulationRunner(new(model.CreateInitialState(input, Math.PI / 2), new IlcaMassModel(), model, input));
        runner.Start();
        Thread.Sleep(10_000); // Deliberately no render reads or frame callbacks for ten seconds.
        runner.Stop();
        var snapshot = runner.Latest;
        Require(snapshot.Fault is null, snapshot.Fault?.Message ?? "ILCA worker failed");
        var expected = snapshot.Timing.ElapsedSeconds * 60;
        Require(Math.Abs(snapshot.State.StepIndex - expected) <= 2, "Cadence deviated by more than two steps");
        Require(snapshot.State.StepIndex >= 590 && !snapshot.Running, "Sustained cadence or shutdown");
        Console.WriteLine($"  Cadence: {snapshot.State.StepIndex} steps / {snapshot.Timing.ElapsedSeconds:F4}s; " +
            $"last step {snapshot.Timing.StepMilliseconds:F3}ms; late {snapshot.Timing.TotalLateSteps}; " +
            $"consecutive {snapshot.Timing.ConsecutiveLateSteps}");
        var p = snapshot.Performance!;
        Console.WriteLine($"  Compute p50/p95/p99/max {p.P50Ms:F3}/{p.P95Ms:F3}/{p.P99Ms:F3}/{p.MaxMs:F3} ms; " +
            $"allocation {p.AllocationBytesPerSecond / 1024:F1} KiB/s; GC {p.Gen0Collections}/{p.Gen1Collections}/{p.Gen2Collections}");
        Require(snapshot.Timing.Warning == DeadlineWarning.None, "Sustained deadline misses are a performance defect");
    })
};

foreach (var (name, run) in tests)
{
    run();
    Console.WriteLine($"PASS {name}");
}
Console.WriteLine($"All {tests.Length} integration groups passed.");

static EnvironmentConfig Environment(int seed) => PresentationConversions.EnvironmentFromSettings(waveSeed: seed);
static double MetersPerSecondToKnots(double value) => PresentationConversions.MetersPerSecondToKnots(value);
static SimulationRunner Make(IForceModel model, SailingControls? controls = null, EnvironmentConfig? environment = null) =>
    new(new Simulation(RigidBodyState.AtRest, new MassProperties(1, new(1, 1, 1)), model,
        new(controls ?? new(), environment ?? Environment(1))));

sealed class AuditModel(SailingControls initial, int mainThreadId) : IForceModel
{
    private SailingControls previous = initial;
    private SimulationInput? stepInput;
    private int stage;
    public int AuditedSteps { get; private set; }
    public int WorkerThreadId { get; private set; }
    public ForceMoment Evaluate(in ForceEvaluation e)
    {
        WorkerThreadId = System.Environment.CurrentManagedThreadId;
        Require(WorkerThreadId != mainThreadId, "Force evaluation on render thread");
        if (stage++ % 4 == 0)
        {
            var direction = e.Input.Environment.WaveSeed;
            Require(direction is 1 or -1, "Torn environment");
            var expected = new HeldSailingInput(direction, direction, direction, direction, direction, direction, direction, direction).Advance(previous);
            Require(expected == e.Input.Controls, "Held keys and environment came from different snapshots");
            previous = e.Input.Controls;
            stepInput = e.Input;
            AuditedSteps++;
        }
        Require(ReferenceEquals(stepInput, e.Input), "Input changed within RK4 step");
        return default;
    }
}

sealed class FaultModel : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation e) => new(new(double.NaN, 0, 0), default);
}

sealed class SlowModel : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation e) { Thread.Sleep(8); return default; }
}

sealed class AllocatingModel : IForceModel
{
    public ForceMoment Evaluate(in ForceEvaluation e)
    { GC.KeepAlive(new byte[4096]); return default; }
}

static class Check
{
    public static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public static void Near(double actual, double expected) => Require(Math.Abs(actual - expected) < 1e-10, $"{actual} != {expected}");
    public static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
    public static void WaitFor(Func<bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate() && timeout.Elapsed.TotalSeconds < 5) Thread.Sleep(1);
        Require(predicate(), "Worker timeout");
    }
}
