using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using System.Diagnostics;
using System.Runtime;

/// <summary>Development-only Hunter host. All physics remains on the simulation worker.</summary>
public partial class HunterDevelopmentScene : Node3D
{
    private SimulationRunner runner = null!;
    private Node3D boat = null!;
    private MeshInstance3D board = null!;
    private MeshInstance3D rudder = null!;
    private MeshInstance3D skipper = null!;
    private readonly ImmediateMesh main = new();
    private readonly ImmediateMesh jib = new();
    private Camera3D camera = null!;
    private Label telemetry = null!;
    private bool smoke;
    private bool scenario;
    private bool scenarioCheck;
    private bool steadyCheck;
    private bool performanceReview;
    private bool reviewFocusLost;
    private int reviewStage = -1;
    private long reviewWindowStart, reviewLastFrame, reviewStartStep, reviewStartLate, reviewMainAllocation;
    private double reviewStartOverrun, reviewStartLag;
    private TimeSpan reviewPauseStart, reviewJitStart, reviewPreviousPause, reviewPreviousJit;
    private double reviewMaxFrame, reviewMaxFramePause, reviewMaxFrameJit;
    private double reviewRenderTotal, reviewRenderMax, reviewTelemetryTotal, reviewTelemetryMax;
    private long reviewPreviousProcessEnd;
    private double reviewMaxFrameOutsideProcess, reviewProcessTotal, reviewProcessMax;
    private RunnerSnapshot? reviewWarmup;
    private readonly List<double> reviewFrames = new(2048);
    private readonly List<(string Name, double Seconds, double[] Frames, RunnerSnapshot Snapshot, long Steps, long Late, double Overrun, double StartLag, double MainKib, double Pause, double Jit, double MaxPause, double MaxJit, double RenderTotal, double RenderMax, double TelemetryTotal, double TelemetryMax, double ProcessTotal, double ProcessMax, double MaxFrameOutside, bool FocusLost)> reviewResults = new(2);
    private double minSteadySpeed = double.PositiveInfinity, maxSteadySpeed;
    private double minSteadyHeading = double.PositiveInfinity, maxSteadyHeading = double.NegativeInfinity;
    private double maxSteadyHeel, maxSteadyLuff;
    private int steadySamples;
    private EnvironmentConfig environment = null!;
    private int scenarioStage = -1;
    private long start;
    private long lastStep = -1;

    public override void _Ready()
    {
        smoke = OS.GetCmdlineUserArgs().Contains("--hunter-smoke");
        scenarioCheck = OS.GetCmdlineUserArgs().Contains("--hunter-scenario-check");
        steadyCheck = OS.GetCmdlineUserArgs().Contains("--hunter-steady-check");
        performanceReview = OS.GetCmdlineUserArgs().Contains("--hunter-performance-review");
        scenario = scenarioCheck || OS.GetCmdlineUserArgs().Contains("--hunter-scenario");
        AddChild(new DirectionalLight3D { RotationDegrees = new(-45, -35, 0), LightEnergy = 1.2f });
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new(0.7f, 0.8f, 0.9f), AmbientLightEnergy = 0.8f
        } });
        boat = new Node3D();
        AddChild(boat);
        environment = HunterDevelopmentScenario.Environment();
        var controls = HunterDevelopmentScenario.InitialControls;
        var simulation = new Hunter235BoatDefinition().CreateSimulation(new(controls, environment), Math.PI / 2);
        var geometry = simulation.Latest.HunterGeometry!;
        BoatPresentation.AddMesh(boat, new BoxMesh { Size = new((float)geometry.HullBeamM, (float)geometry.HullHeightM, (float)geometry.HullLengthM) },
            Vector3.Zero, new(0.85f, 0.87f, 0.82f));
        BoatPresentation.AddMesh(boat, main, Vector3.Zero, new(0.98f, 0.94f, 0.8f));
        BoatPresentation.AddMesh(boat, jib, Vector3.Zero, new(0.88f, 0.91f, 0.82f));
        board = BoatPresentation.AddMesh(boat, new BoxMesh { Size = new(0.06f, 1, 0.55f) }, Vector3.Zero, new(0.45f, 0.35f, 0.2f));
        rudder = BoatPresentation.AddMesh(boat, new BoxMesh { Size = new(0.06f, 1, 0.4f) }, Vector3.Zero, new(0.45f, 0.35f, 0.2f));
        skipper = BoatPresentation.AddMesh(boat, new SphereMesh { Radius = 0.2f, Height = 0.4f }, Vector3.Zero, new(0.9f, 0.25f, 0.1f));
        BoatPresentation.AddMesh(this, new PlaneMesh { Size = new(200, 200) }, Vector3.Zero, new(0.18f, 0.43f, 0.55f));
        camera = new Camera3D { Current = true, Far = 300 };
        AddChild(camera);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        telemetry = new Label { Position = new(16, 16) };
        canvas.AddChild(telemetry);
        runner = new SimulationRunner(simulation);
        Render(simulation.Latest);
        runner.Start();
        start = Stopwatch.GetTimestamp();
    }

    public override void _Process(double delta)
    {
        var frameStart = performanceReview ? Stopwatch.GetTimestamp() : 0;
        var snapshot = runner.Latest;
        if (snapshot.Fault is not null)
        {
            GD.PushError($"Hunter worker stopped: {snapshot.Fault}");
            if (smoke || scenarioCheck || steadyCheck || performanceReview) GetTree().Quit(1);
            return;
        }
        var renderStart = performanceReview ? Stopwatch.GetTimestamp() : 0;
        if (snapshot.State.StepIndex != lastStep) Render(snapshot.State);
        var telemetryStart = performanceReview ? Stopwatch.GetTimestamp() : 0;
        var rig = snapshot.State.HunterRig!;
        var controls = snapshot.State.Input.Controls;
        if (scenario) UpdateScenario(snapshot.State);
        if (steadyCheck) UpdateSteady(snapshot.State);
        if (telemetry.Visible) telemetry.Text = $"HUNTER DEVELOPMENT · provisional physics\n" +
            $"step {snapshot.State.StepIndex}  time {snapshot.State.TimeSeconds:F2} s  worker {snapshot.Timing.StepMilliseconds:F2} ms  late {snapshot.Timing.TotalLateSteps}\n" +
            $"main {rig.Main.Force.ForceWorldN.Length:F1} N  jib {rig.Jib.Force.ForceWorldN.Length:F1} N  total sail {rig.Total.ForceWorldN.Length:F1} N\n" +
            $"jib sheets port {controls.PortJibSheet:F2}  starboard {controls.StarboardJibSheet:F2}  board {controls.Centerboard:F2}  crew {controls.Hiking:F2}\n" +
            $"frame {delta * 1000:F1} ms · {(scenario ? $"script stage {scenarioStage}" : "fixed development inputs")}";
        if (performanceReview && reviewStage is 0 or 1)
        {
            var renderMs = (telemetryStart - renderStart) * 1000.0 / Stopwatch.Frequency;
            var telemetryMs = (Stopwatch.GetTimestamp() - telemetryStart) * 1000.0 / Stopwatch.Frequency;
            reviewRenderTotal += renderMs; reviewRenderMax = Math.Max(reviewRenderMax, renderMs);
            reviewTelemetryTotal += telemetryMs; reviewTelemetryMax = Math.Max(reviewTelemetryMax, telemetryMs);
        }
        if (performanceReview) ReviewPerformance(snapshot, frameStart);
        if (smoke && Stopwatch.GetElapsedTime(start).TotalSeconds > 2)
        {
            var state = snapshot.State;
            var geometry = state.HunterGeometry!;
            var sail = state.HunterRig!;
            var mainVertices = main.GetSurfaceCount() == 1
                ? main.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array() : Array.Empty<Vector3>();
            var jibVertices = jib.GetSurfaceCount() == 1
                ? jib.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array() : Array.Empty<Vector3>();
            if (snapshot.State.StepIndex < 20 || main.GetSurfaceCount() != 1 || jib.GetSurfaceCount() != 1 ||
                !snapshot.Running || lastStep != state.StepIndex ||
                mainVertices.Length == 0 || jibVertices.Length == 0 ||
                mainVertices[0].DistanceTo(SceneFrames.ToGodot(sail.Main.TackBodyM)) > 1e-5 ||
                jibVertices[0].DistanceTo(SceneFrames.ToGodot(sail.Jib.TackBodyM)) > 1e-5 ||
                skipper.Position.DistanceTo(SceneFrames.ToGodot(geometry.SkipperBodyM)) > 1e-5 ||
                board.Position.DistanceTo(SceneFrames.ToGodot((geometry.CenterboardTopBodyM + geometry.CenterboardTipBodyM) * 0.5)) > 1e-5)
            {
                GD.PushError("FAIL Hunter development scene: worker or geometry unavailable");
                GetTree().Quit(1);
                return;
            }
            // The parent ILCA scene must leave focus and F1/F2 handling to this host.
            var parent = (SailingScene)GetParent();
            parent.Notification((int)NotificationApplicationFocusOut);
            parent._Input(new InputEventKey { PhysicalKeycode = Key.F1, Pressed = true });
            parent._Input(new InputEventKey { PhysicalKeycode = Key.F2, Pressed = true });
            var wasVisible = telemetry.Visible;
            _Input(new InputEventKey { PhysicalKeycode = Key.F1, Pressed = true });
            if (telemetry.Visible == wasVisible)
            {
                GD.PushError("FAIL Hunter development scene: telemetry toggle unavailable");
                GetTree().Quit(1);
                return;
            }
            GD.Print($"PASS Hunter development scene: worker snapshots, hull, sails, foils, skipper and telemetry; steps={snapshot.State.StepIndex}");
            GetTree().Quit();
        }
    }

    // Warm up, then observe separate hidden and visible telemetry windows. All
    // sorting, reporting and image readback happen after both timed windows.
    private async void ReviewPerformance(RunnerSnapshot snapshot, long frameStart)
    {
        if (reviewStage == 2) return;
        var now = frameStart;
        if (reviewStage < 0)
        {
            if (Stopwatch.GetElapsedTime(start, now).TotalSeconds < 2) return;
            reviewStage = 0;
            reviewWarmup = snapshot;
            telemetry.Visible = false;
            BeginReviewWindow(snapshot, now);
            return;
        }
        var frameMs = (now - reviewLastFrame) * 1000.0 / Stopwatch.Frequency;
        reviewFrames.Add(frameMs);
        var pause = GC.GetTotalPauseDuration();
        var jit = JitInfo.GetCompilationTime();
        if (frameMs > reviewMaxFrame)
        {
            reviewMaxFrame = frameMs;
            reviewMaxFramePause = (pause - reviewPreviousPause).TotalMilliseconds;
            reviewMaxFrameJit = (jit - reviewPreviousJit).TotalMilliseconds;
            reviewMaxFrameOutsideProcess = (frameStart - reviewPreviousProcessEnd) * 1000.0 / Stopwatch.Frequency;
        }
        reviewPreviousPause = pause;
        reviewPreviousJit = jit;
        reviewLastFrame = now;
        var processEnd = Stopwatch.GetTimestamp();
        var processMs = (processEnd - frameStart) * 1000.0 / Stopwatch.Frequency;
        reviewProcessTotal += processMs;
        reviewProcessMax = Math.Max(reviewProcessMax, processMs);
        reviewPreviousProcessEnd = processEnd;
        var seconds = (now - reviewWindowStart) / (double)Stopwatch.Frequency;
        if (seconds < 10) return;
        reviewResults.Add((reviewStage == 0 ? "hidden" : "visible", seconds, reviewFrames.ToArray(), snapshot,
            snapshot.Timing.CompletedSteps - reviewStartStep, snapshot.Timing.TotalLateSteps - reviewStartLate,
            snapshot.Timing.TotalStartOverrunMilliseconds - reviewStartOverrun, reviewStartLag,
            (GC.GetAllocatedBytesForCurrentThread() - reviewMainAllocation) / 1024.0 / seconds,
            (pause - reviewPauseStart).TotalMilliseconds, (jit - reviewJitStart).TotalMilliseconds,
            reviewMaxFramePause, reviewMaxFrameJit, reviewRenderTotal, reviewRenderMax, reviewTelemetryTotal, reviewTelemetryMax,
            reviewProcessTotal, reviewProcessMax, reviewMaxFrameOutsideProcess, reviewFocusLost));
        if (++reviewStage == 1)
        {
            telemetry.Visible = true;
            BeginReviewWindow(snapshot, Stopwatch.GetTimestamp());
            return;
        }
        runner.Stop();
        var warmup = reviewWarmup!;
        var warmupMetrics = warmup.Performance!;
        GD.Print($"HUNTER WARMUP: steps={warmup.Timing.CompletedSteps} worker elapsed={warmup.Timing.ElapsedSeconds:F2}s " +
            $"late={warmup.Timing.TotalLateSteps} start lag={warmup.Timing.StartLagMilliseconds:F2} ms " +
            $"total/max start overrun={warmup.Timing.TotalStartOverrunMilliseconds:F2}/{warmup.Timing.MaxStartOverrunMilliseconds:F2} ms " +
            $"compute p95/max={warmupMetrics.P95Ms:F2}/{warmupMetrics.MaxMs:F2} ms");
        foreach (var result in reviewResults)
        {
            var sorted = result.Frames.Order().ToArray();
            double Rank(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
            var metrics = result.Snapshot.Performance!;
            GD.Print($"HUNTER PERFORMANCE {result.Name}: focus_lost={result.FocusLost} duration={result.Seconds:F2}s frames={sorted.Length} fps={sorted.Length / result.Seconds:F2} " +
                $"frame p50/p95/p99/max={Rank(.5):F2}/{Rank(.95):F2}/{Rank(.99):F2}/{sorted[^1]:F2} ms; " +
                $"steps={result.Steps} late={result.Late} start lag={result.StartLag:F2}–{result.Snapshot.Timing.StartLagMilliseconds:F2} ms start overrun={result.Overrun:F2} ms " +
                $"max overrun since start={result.Snapshot.Timing.MaxStartOverrunMilliseconds:F2} ms; " +
                $"worker compute p50/p95/p99/max={metrics.P50Ms:F2}/{metrics.P95Ms:F2}/{metrics.P99Ms:F2}/{metrics.MaxMs:F2} ms " +
                $"worker allocation={metrics.AllocationBytesPerSecond / 1024:F1} KiB/s main allocation={result.MainKib:F1} KiB/s " +
                $"process GC={metrics.Gen0Collections}/{metrics.Gen1Collections}/{metrics.Gen2Collections} " +
                $"window GC pause/JIT={result.Pause:F2}/{result.Jit:F2} ms max-frame GC pause/JIT={result.MaxPause:F2}/{result.MaxJit:F2} ms");
            GD.Print($"HUNTER CPU {result.Name}: render mean/max={result.RenderTotal / sorted.Length:F3}/{result.RenderMax:F3} ms " +
                $"telemetry mean/max={result.TelemetryTotal / sorted.Length:F3}/{result.TelemetryMax:F3} ms " +
                $"process mean/max={result.ProcessTotal / sorted.Length:F3}/{result.ProcessMax:F3} ms " +
                $"max-frame gap outside process={result.MaxFrameOutside:F2} ms");
        }
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var path = "/tmp/SailingSimulator-hunter-review.png";
            if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new InvalidOperationException("Screenshot save failed");
            GD.Print($"Hunter review screenshot: {path}. Measurements require manual assessment.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"Hunter review capture failed: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void BeginReviewWindow(RunnerSnapshot snapshot, long now)
    {
        reviewFrames.Clear();
        reviewWindowStart = reviewLastFrame = now;
        reviewPreviousProcessEnd = now;
        reviewStartStep = snapshot.Timing.CompletedSteps;
        reviewStartLate = snapshot.Timing.TotalLateSteps;
        reviewStartOverrun = snapshot.Timing.TotalStartOverrunMilliseconds;
        reviewStartLag = snapshot.Timing.StartLagMilliseconds;
        reviewMainAllocation = GC.GetAllocatedBytesForCurrentThread();
        reviewPauseStart = reviewPreviousPause = GC.GetTotalPauseDuration();
        reviewJitStart = reviewPreviousJit = JitInfo.GetCompilationTime();
        reviewMaxFrame = reviewMaxFramePause = reviewMaxFrameJit = 0;
        reviewRenderTotal = reviewRenderMax = reviewTelemetryTotal = reviewTelemetryMax = 0;
        reviewProcessTotal = reviewProcessMax = reviewMaxFrameOutsideProcess = 0;
        reviewFocusLost = !DisplayServer.WindowIsFocused();
    }

    // Keep a small fixed helm offset through the ordinary held-input interface.
    // Render-thread handoffs can shift the result relative to the exact-step replay.
    private void UpdateSteady(SimulationState state)
    {
        var error = -0.1 - state.Input.Controls.Tiller;
        var direction = Math.Clamp(error / (1.5 * Simulation.StepSeconds), -1, 1);
        if (Math.Abs(direction) < 1e-9) direction = 1e-9; // zero releases the spring-return tiller
        runner.PublishInput(new(new HeldSailingInput(tiller: direction), environment));
        if (state.TimeSeconds < 40) return;
        var attitude = PresentationConversions.GetAttitude(state.Boat.OrientationBodyToWorld);
        var speed = state.Boat.LinearVelocityWorldMps.Length;
        minSteadySpeed = Math.Min(minSteadySpeed, speed); maxSteadySpeed = Math.Max(maxSteadySpeed, speed);
        minSteadyHeading = Math.Min(minSteadyHeading, attitude.Heading); maxSteadyHeading = Math.Max(maxSteadyHeading, attitude.Heading);
        maxSteadyHeel = Math.Max(maxSteadyHeel, Math.Abs(attitude.Heel));
        var rig = state.HunterRig!;
        maxSteadyLuff = Math.Max(maxSteadyLuff, Math.Max(rig.Main.Sections.Average(s => s.Luffing), rig.Jib.Sections.Average(s => s.Luffing)));
        steadySamples++;
        if (state.TimeSeconds < 60) return;
        GD.Print($"Hunter steady reach: samples={steadySamples} speed={minSteadySpeed:F3}–{maxSteadySpeed:F3} m/s heading={minSteadyHeading * 180 / Math.PI:F2}–{maxSteadyHeading * 180 / Math.PI:F2} deg heel<={maxSteadyHeel * 180 / Math.PI:F2} deg luff<={maxSteadyLuff:F3}");
        if (steadySamples < 20 || minSteadySpeed <= 1 || maxSteadySpeed - minSteadySpeed >= 0.05 ||
            maxSteadyHeading - minSteadyHeading >= 2 * Math.PI / 180 || maxSteadyHeel >= Math.PI / 4 || maxSteadyLuff >= 0.1)
        {
            GD.PushError("FAIL Hunter steady reach: host observations did not meet settling criteria");
            GetTree().Quit(1);
            return;
        }
        GD.Print("PASS Hunter steady reach: worker host observations met settling criteria");
        GetTree().Quit();
    }

    // Inputs are published from the presentation thread and sampled by the worker.
    // Boundaries are approximate under frame delays, so this is a host exercise,
    // not an exact deterministic replay or proof that a tack was completed.
    private void UpdateScenario(SimulationState state)
    {
        var t = state.TimeSeconds;
        var stage = HunterDevelopmentScenario.Stage(state.StepIndex);
        var held = HunterDevelopmentScenario.Input(state.StepIndex);
        runner.PublishInput(new(held, environment));
        if (stage == scenarioStage) return;
        scenarioStage = stage;
        var c = state.Input.Controls;
        var forward = state.Boat.OrientationBodyToWorld.Rotate(Vector3d.UnitX);
        GD.Print($"Hunter scenario stage {stage}: step={state.StepIndex} t={t:F2} heading={Math.Atan2(forward.Y, forward.X) * 180 / Math.PI:F1} " +
            $"port={c.PortJibSheet:F2} starboard={c.StarboardJibSheet:F2} board={c.Centerboard:F2} crew={c.Hiking:F2}");
        if (stage == 7 && scenarioCheck)
        {
            if (c.PortJibSheet > 0.1 || c.StarboardJibSheet < 0.6 || c.Centerboard < 0.9 || c.Hiking > -0.9)
            {
                GD.PushError("FAIL Hunter scenario: expected control movements were not observed");
                GetTree().Quit(1);
                return;
            }
            GD.Print("PASS Hunter scenario: worker completed scripted control movements");
            GetTree().Quit();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.F1)
            telemetry.Visible = !telemetry.Visible;
    }

    public override void _Notification(int what)
    {
        if (performanceReview && reviewStage is 0 or 1 && what == NotificationApplicationFocusOut)
            reviewFocusLost = true;
    }

    private void Render(SimulationState state)
    {
        var geometry = state.HunterGeometry!;
        var rig = state.HunterRig!;
        var datum = state.Boat.PositionWorldM - state.Boat.OrientationBodyToWorld.Rotate(state.Mass!.CenterOfMassBodyM);
        boat.Transform = new(new Basis(SceneFrames.ToGodot(state.Boat.OrientationBodyToWorld)), SceneFrames.ToGodot(datum));
        SetFoil(board, geometry.CenterboardTopBodyM, geometry.CenterboardTipBodyM);
        SetFoil(rudder, geometry.RudderTopBodyM, geometry.RudderTipBodyM);
        skipper.Position = SceneFrames.ToGodot(geometry.SkipperBodyM);
        Sail(main, rig.Main);
        Sail(jib, rig.Jib);
        var target = boat.Position + Vector3.Up * 3;
        camera.Position = target + new Vector3(12, 8, 16);
        camera.LookAt(target);
        lastStep = state.StepIndex;
    }

    private static void SetFoil(MeshInstance3D node, Vector3d top, Vector3d tip)
    {
        node.Position = SceneFrames.ToGodot((top + tip) * 0.5);
        node.Scale = new(1, (float)(tip - top).Length, 1);
    }

    private static void Sail(ImmediateMesh mesh, HunterSailState sail)
    {
        var rows = new[] { new HunterSailSectionGeometry(sail.TackBodyM, sail.ClewBodyM) }
            .Concat(sail.Geometry).Append(new(sail.HeadBodyM, sail.HeadBodyM)).ToArray();
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < rows.Length - 1; i++)
        {
            var a = SceneFrames.ToGodot(rows[i].LeadingEdgeBodyM);
            var b = SceneFrames.ToGodot(rows[i].TrailingEdgeBodyM);
            var c = SceneFrames.ToGodot(rows[i + 1].LeadingEdgeBodyM);
            var d = SceneFrames.ToGodot(rows[i + 1].TrailingEdgeBodyM);
            BoatPresentation.Triangle(mesh, a, b, c);
            BoatPresentation.Triangle(mesh, b, d, c);
        }
        mesh.SurfaceEnd();
    }

    public override void _ExitTree() => runner?.Dispose();
}
