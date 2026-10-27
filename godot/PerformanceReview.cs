using Godot;
using SailingSimulator.Adapters;
using System.Diagnostics;
using System.Runtime;

public partial class SailingScene
{
    private int performanceStage = -1;
    private long stageLateSteps;
    private double stageStartOverrun;
    private readonly string[] performanceNames = ["hidden", "telemetry", "environment", "performance"];
    private bool performanceCapturing;
    private int captureFrame;
    private int captureStage;
    private List<double> frameTimes => performanceResults[performanceStage].Frames;
    private List<double> renderCosts => performanceResults[performanceStage].Render;
    private List<double> overlayCosts => performanceResults[performanceStage].Overlay;
    private double measuredSeconds;
    private long mainAllocationStart;
    private TimeSpan pauseStart;
    private TimeSpan jitStart;
    private double maximumFrameMs;
    private double pauseAtMaximumMs;
    private double jitAtMaximumMs;
    private TimeSpan previousPause;
    private TimeSpan previousJit;
    private readonly PerformanceResult[] performanceResults =
        [new("hidden"), new("telemetry"), new("environment"), new("performance")];
    private sealed class PerformanceResult(string name)
    {
        public string Name { get; } = name;
        public List<double> Frames { get; } = new(512);
        public List<double> Render { get; } = new(512);
        public List<double> Overlay { get; } = new(512);
        public RunnerSnapshot Snapshot { get; set; } = null!;
        public long LateSteps { get; set; }
        public double MainKiBPerSecond { get; set; }
        public double PauseMs { get; set; }
        public double JitMs { get; set; }
        public double MaxFramePauseMs { get; set; }
        public double MaxFrameJitMs { get; set; }
        public double StartOverrunMs { get; set; }
    }

    private void ObservePerformanceFrame(double renderMs, double overlayMs)
    {
        if (performanceStage < 0 || performanceStage >= performanceNames.Length) return;
        renderCosts.Add(renderMs);
        overlayCosts.Add(overlayMs);
    }

    private void ObservePerformanceInterval(double seconds)
    {
        if (performanceStage < 0 || performanceStage >= performanceNames.Length) return;
        // These counters and the frame timestamp share the same frame-start boundary.
        var pause = GC.GetTotalPauseDuration();
        var jit = JitInfo.GetCompilationTime();
        if (seconds * 1000 > maximumFrameMs)
        {
            maximumFrameMs = seconds * 1000;
            pauseAtMaximumMs = (pause - previousPause).TotalMilliseconds;
            jitAtMaximumMs = (jit - previousJit).TotalMilliseconds;
        }
        previousPause = pause;
        previousJit = jit;
    }

    private void BeginPerformanceWindow(RunnerSnapshot snapshot)
    {
        stageLateSteps = snapshot.Timing.TotalLateSteps;
        stageStartOverrun = snapshot.Timing.TotalStartOverrunMilliseconds;
        mainAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        pauseStart = previousPause = GC.GetTotalPauseDuration();
        jitStart = previousJit = JitInfo.GetCompilationTime();
        maximumFrameMs = pauseAtMaximumMs = jitAtMaximumMs = 0;
        measuredSeconds = 0;
    }

    /// <summary>Live measurements finish before any GPU readback. Capture cannot pollute measured cadence.</summary>
    private async void ProcessPerformanceReview(RunnerSnapshot snapshot, double delta)
    {
        if (performanceCapturing) return;
        if (performanceStage == performanceNames.Length)
        {
            if (++captureFrame < 12) return;
            performanceCapturing = true;
            try
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng($"/tmp/SailingSimulator-debug-{performanceNames[captureStage]}.png") == Error.Ok,
                    "debug review capture");
                if (++captureStage == performanceNames.Length)
                {
                    GD.Print("PASS performance review capture (measurements above; no automatic FPS acceptance)");
                    GetTree().Quit();
                    return;
                }
                debug.Visible = true;
                debug.Tabs.CurrentTab = captureStage - 1;
                captureFrame = 0;
                performanceCapturing = false;
            }
            catch (Exception exception)
            {
                GD.PushError($"FAIL performance review capture: {exception.Message}");
                GetTree().Quit(1);
            }
            return;
        }
        var elapsed = Stopwatch.GetElapsedTime(smokeStart).TotalSeconds;
        if (elapsed < 2) return;
        if (performanceStage < 0)
        {
            performanceStage = 0;
            BeginPerformanceWindow(snapshot);
            return;
        }
        frameTimes.Add(delta * 1000);
        measuredSeconds += delta;
        if (elapsed < 2 + (performanceStage + 1) * 5) return;
        performanceCapturing = true;
        try
        {
            // Keep reporting/JIT/GPU readback outside all measured windows.
            var result = performanceResults[performanceStage];
            result.Snapshot = snapshot;
            result.LateSteps = snapshot.Timing.TotalLateSteps - stageLateSteps;
            result.StartOverrunMs = snapshot.Timing.TotalStartOverrunMilliseconds - stageStartOverrun;
            result.MainKiBPerSecond = (GC.GetAllocatedBytesForCurrentThread() - mainAllocationStart) / 1024.0 / measuredSeconds;
            result.PauseMs = (GC.GetTotalPauseDuration() - pauseStart).TotalMilliseconds;
            result.JitMs = (JitInfo.GetCompilationTime() - jitStart).TotalMilliseconds;
            result.MaxFramePauseMs = pauseAtMaximumMs;
            result.MaxFrameJitMs = jitAtMaximumMs;
            if (++performanceStage == performanceNames.Length)
            {
                runner!.Stop();
                Require(runner.Latest.Fault is null, "worker fault during performance review");
                PrintPerformanceResults();
                debug.Visible = false;
                performanceCapturing = false;
                return;
            }
            debug.Visible = true;
            debug.Tabs.CurrentTab = performanceStage - 1;
            BeginPerformanceWindow(snapshot);
            performanceCapturing = false;
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL performance review: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void PrintPerformanceResults()
    {
        foreach (var result in performanceResults)
        {
            var sorted = result.Frames.Order().ToArray();
            double Rank(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
            var snapshot = result.Snapshot;
            var metrics = snapshot.Performance!;
            GD.Print($"PERFORMANCE {result.Name}: frames={sorted.Length}, fps={1000 / sorted.Average():F2}, " +
                $"frame p50/p95/p99/max={Rank(.5):F2}/{Rank(.95):F2}/{Rank(.99):F2}/{sorted[^1]:F2} ms; " +
                $"step compute p50/p95/p99/max={metrics.P50Ms:F3}/{metrics.P95Ms:F3}/{metrics.P99Ms:F3}/{metrics.MaxMs:F3} ms; " +
                $"late={result.LateSteps}, consecutive={snapshot.Timing.ConsecutiveLateSteps}, " +
                $"allocation={metrics.AllocationBytesPerSecond / 1024:F1} KiB/s, GC={metrics.Gen0Collections}/{metrics.Gen1Collections}/{metrics.Gen2Collections}");
            GD.Print($"ATTRIBUTION {result.Name}: render mean/max={result.Render.Average():F3}/{result.Render.Max():F3} ms, " +
                $"overlay mean/max={result.Overlay.Average():F3}/{result.Overlay.Max():F3} ms, " +
                $"main allocation={result.MainKiBPerSecond:F1} KiB/s, process GC pause={result.PauseMs:F2} ms, " +
                $"process JIT={result.JitMs:F2} ms; max-frame GC pause/JIT={result.MaxFramePauseMs:F2}/{result.MaxFrameJitMs:F2} ms, " +
                $"worker lateness={snapshot.Timing.DeadlineLatenessMilliseconds:F2} ms, " +
                $"start lag={snapshot.Timing.StartLagMilliseconds:F2} ms, window start overrun={result.StartOverrunMs:F2} ms, " +
                $"max start overrun since start={snapshot.Timing.MaxStartOverrunMilliseconds:F2} ms");
        }
    }
}
