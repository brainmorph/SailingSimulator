using System.Diagnostics;
using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

public enum DeadlineWarning { None, Soft, Hard }

public sealed record StepTiming(long CompletedSteps, double ElapsedSeconds, double StepMilliseconds,
    double DeadlineLatenessMilliseconds, int ConsecutiveLateSteps, long TotalLateSteps)
{
    public DeadlineWarning Warning => ConsecutiveLateSteps >= 5 ? DeadlineWarning.Hard
        : ConsecutiveLateSteps >= 3 ? DeadlineWarning.Soft : DeadlineWarning.None;
}

/// <summary>State and timing always describe the same completed step. A fault retains that state.</summary>
public sealed record RunnerSnapshot(SimulationState State, StepTiming Timing, bool Running, Exception? Fault = null)
{
    public PerformanceSnapshot? Performance { get; init; }
}

/// <summary>
/// Absolute 60 Hz deadlines, with a minimum period between actual starts to prevent catch-up bursts.
/// Deadlines never reset after a miss; sustained overload remains visible as increasing lateness.
/// </summary>
public sealed class FixedStepSchedule
{
    private readonly long epoch;
    private readonly long frequency;
    private long previousStart;
    private long steps;
    public FixedStepSchedule(long epoch, long frequency)
    {
        if (frequency < 60) throw new ArgumentOutOfRangeException(nameof(frequency));
        this.epoch = epoch;
        this.frequency = frequency;
        previousStart = epoch;
    }

    public long NextStart => steps == 0 ? epoch : Math.Max(epoch + Offset(steps), previousStart + Offset(1));
    public long BeginStep(long actualStart)
    {
        if (actualStart < NextStart) throw new ArgumentOutOfRangeException(nameof(actualStart));
        previousStart = actualStart;
        return checked(epoch + Offset(++steps));
    }

    private long Offset(long index) => checked((long)Math.Round(index * (double)frequency / 60));
}

/// <summary>
/// Single-use dedicated simulation owner. Callers transfer exclusive ownership of the supplied solver.
/// No engine types, scene calls, callbacks or render-clock dependencies enter the worker.
/// </summary>
public sealed class SimulationRunner : IDisposable
{
    private readonly Simulation simulation;
    private readonly SnapshotMailbox<HostInput> input;
    private readonly SnapshotMailbox<RunnerSnapshot> output;
    private readonly ManualResetEventSlim stop = new(false);
    private readonly object lifecycle = new();
    private Thread? worker;
    private bool stopped;
    private bool disposed;

    public RunnerSnapshot Latest => output.Read();

    public SimulationRunner(Simulation simulation)
    {
        this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        input = new(new(HeldSailingInput.Released, simulation.Latest.Input.Environment));
        output = new(new(simulation.Latest, new(0, 0, 0, 0, 0, 0), false));
    }

    public void PublishInput(HostInput snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Held);
        ArgumentNullException.ThrowIfNull(snapshot.Environment);
        input.Publish(snapshot);
    }

    public void Start()
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker is not null || stopped) throw new InvalidOperationException("Create a new runner for each session.");
            output.Publish(Latest with { Running = true });
            worker = new Thread(Run) { IsBackground = true, Name = "Sailing simulation (60 Hz)" };
            worker.Start();
        }
    }

    public void Stop()
    {
        lock (lifecycle)
        {
            if (disposed) return;
            stopped = true;
            stop.Set();
            worker?.Join();
        }
    }

    public void Dispose()
    {
        lock (lifecycle)
        {
            if (disposed) return;
            Stop();
            stop.Dispose();
            disposed = true;
        }
    }

    private void Run()
    {
        var epoch = Stopwatch.GetTimestamp();
        var schedule = new FixedStepSchedule(epoch, Stopwatch.Frequency);
        var consecutiveLate = 0;
        long totalLate = 0, completed = 0;
        var profile = new StepProfiler();
        var performance = new StepPerformanceWindow();
        var previousSample = epoch;
        var previousAllocation = GC.GetAllocatedBytesForCurrentThread();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        try
        {
            while (WaitUntil(schedule.NextStart))
            {
                var started = Stopwatch.GetTimestamp();
                var deadline = schedule.BeginStep(started);
                var current = input.Read(); // Exactly one coherent held-key/environment sample per step.
                var controls = current.Held.Advance(simulation.Latest.Input.Controls);
                profile.Reset();
                var state = simulation.Step(new(controls, current.Environment), profile);
                var computeFinished = Stopwatch.GetTimestamp();
                var allocation = GC.GetAllocatedBytesForCurrentThread();
                var metrics = performance.Add((computeFinished - started) * 1000.0 / Stopwatch.Frequency,
                    allocation - previousAllocation, (computeFinished - previousSample) / (double)Stopwatch.Frequency,
                    GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2,
                    GC.GetTotalMemory(false), profile.Snapshot());
                previousSample = computeFinished;
                previousAllocation = allocation;
                var finished = Stopwatch.GetTimestamp();
                var lateness = Math.Max(0, finished - deadline);
                consecutiveLate = lateness > 0 ? consecutiveLate + 1 : 0;
                if (lateness > 0) totalLate++;
                var timing = new StepTiming(++completed, (finished - epoch) / (double)Stopwatch.Frequency,
                    (finished - started) * 1000.0 / Stopwatch.Frequency,
                    lateness * 1000.0 / Stopwatch.Frequency, consecutiveLate, totalLate);
                output.Publish(new(state, timing, true) { Performance = metrics });
            }
        }
        catch (Exception exception)
        {
            output.Publish(Latest with { Running = false, Fault = exception });
        }
        finally
        {
            output.Publish(Latest with { Running = false });
        }
    }

    private bool WaitUntil(long timestamp)
    {
        while (!stop.IsSet)
        {
            var remaining = (timestamp - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
            if (remaining <= 0) return true;
            // Sleep for most of the interval; a short final spin avoids millisecond timer quantization.
            if (remaining > 0.002) stop.Wait(TimeSpan.FromSeconds(remaining - 0.001));
            else Thread.SpinWait(32);
        }
        return false;
    }
}
