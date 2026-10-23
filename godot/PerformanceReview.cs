using Godot;
using SailingSimulator.Adapters;
using System.Diagnostics;

public partial class SailingScene
{
    private int performanceStage = -1;
    private readonly List<double> frameTimes = new();
    private long stageLateSteps;
    private readonly string[] performanceNames = ["hidden", "telemetry", "environment", "performance"];
    private bool performanceCapturing;
    private int captureFrame;
    private int captureStage;

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
            stageLateSteps = snapshot.Timing.TotalLateSteps;
            return;
        }
        frameTimes.Add(delta * 1000);
        if (elapsed < 2 + (performanceStage + 1) * 5) return;
        performanceCapturing = true;
        try
        {
            var sorted = frameTimes.Order().ToArray();
            double Rank(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
            var metrics = snapshot.Performance!;
            GD.Print($"PERFORMANCE {performanceNames[performanceStage]}: frames={sorted.Length}, fps={1000 / sorted.Average():F2}, " +
                $"frame p50/p95/p99/max={Rank(.5):F2}/{Rank(.95):F2}/{Rank(.99):F2}/{sorted[^1]:F2} ms; " +
                $"step compute p50/p95/p99/max={metrics.P50Ms:F3}/{metrics.P95Ms:F3}/{metrics.P99Ms:F3}/{metrics.MaxMs:F3} ms; " +
                $"late={snapshot.Timing.TotalLateSteps - stageLateSteps}, consecutive={snapshot.Timing.ConsecutiveLateSteps}, " +
                $"allocation={metrics.AllocationBytesPerSecond / 1024:F1} KiB/s, GC={metrics.Gen0Collections}/{metrics.Gen1Collections}/{metrics.Gen2Collections}");
            if (++performanceStage == performanceNames.Length)
            {
                runner!.Stop();
                Require(runner.Latest.Fault is null, "worker fault during performance review");
                debug.Visible = false;
                performanceCapturing = false;
                return;
            }
            debug.Visible = true;
            debug.Tabs.CurrentTab = performanceStage - 1;
            frameTimes.Clear();
            stageLateSteps = snapshot.Timing.TotalLateSteps;
            performanceCapturing = false;
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL performance review: {exception.Message}");
            GetTree().Quit(1);
        }
    }
}
