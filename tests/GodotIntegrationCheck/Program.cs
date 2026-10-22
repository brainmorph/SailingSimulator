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
        Require(snapshot.Timing.Warning == DeadlineWarning.None, "Sustained deadline misses are a performance defect");
        Console.WriteLine($"  Cadence: {snapshot.State.StepIndex} steps / {snapshot.Timing.ElapsedSeconds:F4}s; " +
            $"last step {snapshot.Timing.StepMilliseconds:F3}ms; late {snapshot.Timing.TotalLateSteps}; " +
            $"consecutive {snapshot.Timing.ConsecutiveLateSteps}");
    })
};

foreach (var (name, run) in tests)
{
    run();
    Console.WriteLine($"PASS {name}");
}
Console.WriteLine($"All {tests.Length} integration groups passed.");

static EnvironmentConfig Environment(int seed) => PresentationConversions.EnvironmentFromSettings(waveSeed: seed);
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
