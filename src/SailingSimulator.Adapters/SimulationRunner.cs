using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

public enum DeadlineWarning { None, Soft, Hard }

public sealed record StepTiming(long CompletedSteps, double ElapsedSeconds, double StepMilliseconds,
    double DeadlineLatenessMilliseconds, int ConsecutiveLateSteps, long TotalLateSteps)
{
    // Start lag is relative to the original cadence; overrun is relative to the
    // requested start, and includes both wake-up delays and prior step work.
    public double StartLagMilliseconds { get; init; }
    public double TotalStartOverrunMilliseconds { get; init; }
    public double MaxStartOverrunMilliseconds { get; init; }
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
    private WindowsHighResolutionTimer? timer;
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
            timer?.Dispose();
            disposed = true;
        }
    }

    private void Run()
    {
        var epoch = Stopwatch.GetTimestamp();
        var schedule = new FixedStepSchedule(epoch, Stopwatch.Frequency);
        var consecutiveLate = 0;
        long totalLate = 0, completed = 0;
        long totalStartOverrun = 0, maxStartOverrun = 0;
        var profile = new StepProfiler();
        var performance = new StepPerformanceWindow();
        var previousSample = epoch;
        var previousAllocation = GC.GetAllocatedBytesForCurrentThread();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        try
        {
            while (true)
            {
                var requestedStart = schedule.NextStart;
                if (!WaitUntil(requestedStart)) break;
                var started = Stopwatch.GetTimestamp();
                var deadline = schedule.BeginStep(started);
                var overrun = started - requestedStart;
                totalStartOverrun += overrun;
                maxStartOverrun = Math.Max(maxStartOverrun, overrun);
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
                var nominalStart = epoch + (long)Math.Round(completed * (double)Stopwatch.Frequency / 60);
                consecutiveLate = lateness > 0 ? consecutiveLate + 1 : 0;
                if (lateness > 0) totalLate++;
                var timing = new StepTiming(++completed, (finished - epoch) / (double)Stopwatch.Frequency,
                    (finished - started) * 1000.0 / Stopwatch.Frequency,
                    lateness * 1000.0 / Stopwatch.Frequency, consecutiveLate, totalLate)
                {
                    StartLagMilliseconds = (started - nominalStart) * 1000.0 / Stopwatch.Frequency,
                    TotalStartOverrunMilliseconds = totalStartOverrun * 1000.0 / Stopwatch.Frequency,
                    MaxStartOverrunMilliseconds = maxStartOverrun * 1000.0 / Stopwatch.Frequency
                };
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
            if (remaining > 0.002) Sleep(remaining - 0.001);
            else Thread.SpinWait(32);
        }
        return false;
    }

    private void Sleep(double seconds)
    {
        if (!OperatingSystem.IsWindows() || !(timer ??= WindowsHighResolutionTimer.TryCreate()).Arm(seconds))
            stop.Wait(TimeSpan.FromSeconds(seconds));
        else WaitHandle.WaitAny([timer, stop.WaitHandle]);
    }
}

/// <summary>
/// Default Windows sleeps round up to ~15.6 ms quanta, overshooting every 60 Hz deadline, and
/// timeBeginPeriod is ignored for windowless processes. High-resolution waitable timers are not.
/// </summary>
internal sealed class WindowsHighResolutionTimer : WaitHandle
{
    private const uint HighResolution = 0x2, AllAccess = 0x1F0003;

    public static WindowsHighResolutionTimer TryCreate()
    {
        var timer = new WindowsHighResolutionTimer();
        timer.SafeWaitHandle = new SafeWaitHandle(CreateWaitableTimerExW(IntPtr.Zero, null, HighResolution, AllAccess), true);
        return timer;
    }

    /// <summary>False when the timer is unavailable (Windows before 10 1803), so callers fall back.</summary>
    public bool Arm(double seconds)
    {
        if (SafeWaitHandle.IsInvalid) return false;
        var dueTime = -(long)(seconds * 10_000_000); // Negative: relative, in 100 ns units.
        return SetWaitableTimer(SafeWaitHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period,
        IntPtr completionRoutine, IntPtr completionArgument, [MarshalAs(UnmanagedType.Bool)] bool resume);
}
