using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using System.Diagnostics;

/// <summary>Only this main-thread adapter reads input or touches Godot nodes.</summary>
public partial class SailingScene : Node3D
{
    private SailingSession? session;
    private SimulationRunner? runner => session?.Runner;
    private bool hunterMode;
    private BoatPresentation ilcaBoat = null!;
    private HunterPresentation hunterBoat = null!;
    private Node3D activeBoat = null!;
    private IBoatView view = null!;
    private Control picker = null!;
    private WaterPresentation water = null!;
    private Camera3D camera = null!;
    private DebugOverlay debug = null!;
    private Control controlsHelp = null!;
    private EnvironmentConfig environment = null!;
    private bool focused = true;
    private bool orbiting;
    private float cameraYaw = -0.75f;
    private float cameraPitch = 0.28f;
    private float cameraDistance = 10.5f;
    private bool onboard;
    private float onboardYaw;
    private float onboardPitch = 0.08f;
    private float onboardFov = 80;
    private long visualStep = -1;
    private bool smokeTest;
    private bool pickerCheck;
    private bool performanceReview;
    private bool stalled;
    private bool released;
    private long smokeStart;
    private long previousRenderedStep;
    private long previousFrameTimestamp;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--hunter-dev") || OS.GetCmdlineUserArgs().Contains("--hunter-smoke") ||
            OS.GetCmdlineUserArgs().Contains("--hunter-scenario") || OS.GetCmdlineUserArgs().Contains("--hunter-scenario-check") ||
            OS.GetCmdlineUserArgs().Contains("--hunter-steady-check") || OS.GetCmdlineUserArgs().Contains("--hunter-performance-review"))
        {
            hunterMode = true;
            AddChild(new HunterDevelopmentScene());
            return;
        }
        SailingKeyMap.Register();
        smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        pickerCheck = OS.GetCmdlineUserArgs().Contains("--picker-check");
        performanceReview = OS.GetCmdlineUserArgs().Contains("--performance-review");
        if (smokeTest)
        {
            try { VerifyKeyBindings(); }
            catch (Exception exception)
            {
                GD.PushError($"FAIL Godot key bindings: {exception.Message}");
                GetTree().Quit(1);
                return;
            }
        }
        BuildScene();
        if (OS.GetCmdlineUserArgs().Contains("--visual-review"))
        {
            StartVisualReview();
            return;
        }
        environment = PresentationConversions.EnvironmentFromSettings(waveSeed: smokeTest || performanceReview ? 123 : null);
        debug.Initialize(environment, changed => environment = changed);
        var requested = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--boat=", StringComparison.Ordinal));
        if (requested is not null && !BoatCatalog.TryParse(requested["--boat=".Length..], out _))
            GD.PushError($"Unknown boat {requested}; choose ilca-7 or hunter-235.");
        // Automated checks start with the ILCA; otherwise the player chooses before sailing.
        if (BoatCatalog.TryParse(requested?["--boat=".Length..], out var boatChoice) || smokeTest || performanceReview)
            StartSession(boatChoice);
        else
            picker.Visible = true;
        smokeStart = Stopwatch.GetTimestamp();
        if (smokeTest)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
        }
    }

    /// <summary>Starts the chosen boat, or restarts the session; the previous worker is joined first.</summary>
    private void StartSession(BoatChoice choice)
    {
        SailingKeyMap.ReleaseAll();
        orbiting = false;
        var changed = session?.Boat != choice;
        session = session is null ? SailingSession.Create(choice, environment) : session.Restart(choice, environment);
        ilcaBoat.Visible = choice == BoatChoice.Ilca7;
        hunterBoat.Visible = choice == BoatChoice.Hunter235;
        activeBoat = choice == BoatChoice.Ilca7 ? ilcaBoat : hunterBoat;
        view = choice == BoatChoice.Ilca7 ? ilcaBoat : hunterBoat;
        // Each boat starts with its own fitted third-person distance; a same-boat restart keeps the zoom.
        if (changed) cameraDistance = view.CameraDistance;
        water.WakeStartHalfWidthM = view.WakeStartHalfWidthM;
        visualStep = -1;
        previousRenderedStep = 0;
        water.Trail.Clear();
        debug.ResetHistory();
        debug.InitializeSession(choice, StartSession);
        ControlsHelp.Show(controlsHelp, choice);
        picker.Visible = false;
        GetViewport().GuiReleaseFocus();
        Render(session.Runner.Latest.State);
        session.Runner.Start();
    }

    public override void _Process(double delta)
    {
        if (visualReview) { ProcessVisualReview(); return; }
        if (pickerCheck) { CheckPicker(); return; }
        if (runner is null) return;
        var frameTimestamp = Stopwatch.GetTimestamp();
        var frameSeconds = previousFrameTimestamp == 0 ? delta
            : Stopwatch.GetElapsedTime(previousFrameTimestamp, frameTimestamp).TotalSeconds;
        previousFrameTimestamp = frameTimestamp;
        if (performanceReview) ObservePerformanceInterval(frameSeconds);
        runner.PublishInput(new(focused ? SailingKeyMap.Read(session!.Boat == BoatChoice.Hunter235) : HeldSailingInput.Released, environment));
        var snapshot = runner.Latest; // One completed snapshot drives the whole render frame.
        var renderStart = performanceReview ? Stopwatch.GetTimestamp() : 0;
        Render(snapshot.State);
        var overlayStart = performanceReview ? Stopwatch.GetTimestamp() : 0;
        debug.Observe(snapshot, frameSeconds);
        if (smokeTest) SmokeCheck(snapshot);
        if (performanceReview)
        {
            ObservePerformanceFrame(Stopwatch.GetElapsedTime(renderStart, overlayStart).TotalMilliseconds,
                Stopwatch.GetElapsedTime(overlayStart).TotalMilliseconds);
            ProcessPerformanceReview(snapshot, frameSeconds);
        }
    }

    private void Render(SimulationState state)
    {
        if (state.StepIndex != visualStep)
        {
            view.Render(state);
            water.Render(state, activeBoat.Position);
            visualStep = state.StepIndex;
        }
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        view.Sailor.Visible = !onboard;
        if (onboard)
        {
            camera.Fov = onboardFov;
            camera.Near = 0.04f;
            camera.Position = activeBoat.ToGlobal(view.Sailor.Position + view.EyeOffset);
            var direction = new Vector3(Mathf.Sin(onboardYaw) * Mathf.Cos(onboardPitch),
                Mathf.Sin(onboardPitch), -Mathf.Cos(onboardYaw) * Mathf.Cos(onboardPitch));
            camera.LookAt(camera.Position + activeBoat.Basis * direction, activeBoat.Basis * Vector3.Up);
        }
        else
        {
            camera.Fov = 65;
            camera.Near = 0.1f;
            var target = activeBoat.Position + Vector3.Up * view.CameraTargetHeight;
            var forward = -activeBoat.Basis.Z;
            var heading = Mathf.Atan2(-forward.X, -forward.Z);
            var yaw = cameraYaw + heading;
            camera.Position = target + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(cameraPitch),
                Mathf.Sin(cameraPitch), Mathf.Cos(yaw) * Mathf.Cos(cameraPitch)) * cameraDistance;
            camera.LookAt(target);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (hunterMode) return;
        if (@event is InputEventKey diagnosticKey && (diagnosticKey.PhysicalKeycode == Key.F1 || diagnosticKey.Keycode == Key.F1) && focused)
        {
            if (diagnosticKey.Pressed && !diagnosticKey.Echo)
            {
                debug.Visible = !debug.Visible;
                orbiting = false;
                if (!debug.Visible) GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
            }
            GetViewport().SetInputAsHandled();
        }
        if (@event is InputEventKey key && (key.PhysicalKeycode == Key.F2 || key.Keycode == Key.F2) && focused)
        {
            if (key.Pressed && !key.Echo) controlsHelp.Visible = !controlsHelp.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (hunterMode) return;
        if (@event is InputEventKey key && key.PhysicalKeycode == Key.C && key.Pressed && !key.Echo && focused)
            onboard = !onboard;
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right) orbiting = button.Pressed && focused;
            if (button.Pressed && focused)
            {
                var zoom = button.ButtonIndex == MouseButton.WheelUp ? -1 : button.ButtonIndex == MouseButton.WheelDown ? 1 : 0;
                if (onboard) onboardFov = Mathf.Clamp(onboardFov + zoom * 3, 55, 95);
                else cameraDistance = Mathf.Clamp(cameraDistance + zoom, 4, 40);
            }
        }
        if (@event is InputEventMouseMotion motion && orbiting)
        {
            if (onboard)
            {
                onboardYaw = Mathf.Clamp(onboardYaw - motion.Relative.X * 0.005f, -2.6f, 2.6f);
                onboardPitch = Mathf.Clamp(onboardPitch + motion.Relative.Y * 0.005f, -0.5f, 1.3f);
            }
            else
            {
                cameraYaw -= motion.Relative.X * 0.005f;
                cameraPitch = Mathf.Clamp(cameraPitch + motion.Relative.Y * 0.005f, 0.08f, 1.35f);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (hunterMode) return;
        if (what == NotificationApplicationFocusOut)
        {
            focused = false;
            orbiting = false;
            SailingKeyMap.ReleaseAll();
            runner?.PublishInput(new(HeldSailingInput.Released, environment));
        }
        else if (what == NotificationApplicationFocusIn) focused = true;
    }

    public override void _ExitTree()
    {
        session?.Dispose(); // Wake the deadline wait and join before nodes disappear.
        session = null;
    }

    private void BuildScene()
    {
        var light = new DirectionalLight3D { RotationDegrees = new(-45, -35, 0), LightEnergy = 1.2f };
        AddChild(light);
        var world = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new(0.65f, 0.76f, 0.85f), AmbientLightEnergy = 0.65f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        };
        AddChild(world);
        ilcaBoat = new BoatPresentation { Name = "Boat" };
        AddChild(ilcaBoat);
        ilcaBoat.Build();
        hunterBoat = new HunterPresentation { Name = "Hunter", Visible = false };
        AddChild(hunterBoat);
        hunterBoat.Build(Hunter235Geometry.ForControls(BoatCatalog.InitialControls(BoatChoice.Hunter235)));
        activeBoat = ilcaBoat;
        view = ilcaBoat;
        water = new WaterPresentation { Name = "Water" };
        AddChild(water);
        water.Build();
        camera = new Camera3D { Current = true, Far = 1800 };
        AddChild(camera);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        canvas.AddChild(new Label
        {
            Text = "F1  Debug    ·    F2  Controls    ·    C  Camera    ·    Right drag  Look    ·    Wheel  Zoom",
            Position = new(16, 680), Modulate = new(0.9f, 0.94f, 0.96f, 0.8f)
        });
        debug = new DebugOverlay();
        debug.Build();
        canvas.AddChild(debug);
        controlsHelp = ControlsHelp.Create();
        canvas.AddChild(controlsHelp);
        controlsHelp.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        picker = BoatPicker.Create(StartSession);
        canvas.AddChild(picker);
        picker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void SmokeCheck(RunnerSnapshot snapshot)
    {
        try
        {
            Require(snapshot.Fault is null, "worker fault");
            Require(snapshot.State.StepIndex >= previousRenderedStep, "snapshot order");
            previousRenderedStep = snapshot.State.StepIndex;
            var datum = snapshot.State.Boat.PositionWorldM - snapshot.State.Boat.OrientationBodyToWorld.Rotate(snapshot.State.Mass!.CenterOfMassBodyM);
            Require(activeBoat.Position.DistanceTo(SceneFrames.ToGodot(datum)) < 0.0001, "rendered hull datum");
            Require(Math.Abs(activeBoat.Quaternion.Dot(SceneFrames.ToGodot(snapshot.State.Boat.OrientationBodyToWorld))) > 0.9999, "rendered orientation");
            var elapsed = Stopwatch.GetElapsedTime(smokeStart).TotalSeconds;
            if (!stalled && elapsed > 0.4)
            {
                Require(snapshot.State.Input.Controls.Tiller > 0.1 && snapshot.State.Input.Controls.Mainsheet > 0.6, "held physical key input");
                var keys = SailingKeyMap.Read();
                VerifyDebugControls(keys);
                Require(!controlsHelp.Visible, "controls help starts hidden");
                SendKey(Key.F2, true);
                Require(controlsHelp.Visible, "F2 opens controls help");
                Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F2, Pressed = true, Echo = true });
                Input.FlushBufferedEvents();
                Require(controlsHelp.Visible, "held F2 does not repeatedly toggle help");
                SendKey(Key.F2, false);
                Require(controlsHelp.Visible && SailingKeyMap.Read() == keys, "help preserves held sailing keys");
                var step = runner!.Latest.State.StepIndex;
                Thread.Sleep(250); // Deliberate main-thread stall: the worker must keep stepping.
                Require(runner.Latest.State.StepIndex >= step + 10, "render-independent stepping");
                SendKey(Key.F2, true);
                SendKey(Key.F2, false);
                Require(!controlsHelp.Visible && SailingKeyMap.Read() == keys, "F2 closes help without changing sailing input");
                stalled = true;
                _Notification((int)NotificationApplicationFocusOut);
                released = true;
            }
            if (released && elapsed > 1.5)
            {
                Require(snapshot.State.Input.Environment.WaveSeed == -456 &&
                    snapshot.State.Input.Environment.WavePreset == WavePreset.Moderate &&
                    snapshot.State.Input.Environment.WaveDirectionMode == WaveDirectionMode.Independent,
                    "F1 settings reach the completed physics snapshot");
                Require(snapshot.State.Input.Environment.TrueWindVelocityWorldMps == environment.TrueWindVelocityWorldMps,
                    "active wind matches F1 input");
                Require(snapshot.Performance is { Samples: > 0 } && snapshot.Performance.Subsystems.HullMs > 0,
                    "performance and subsystem metrics published");
                debug.Visible = true;
                debug.Observe(snapshot, 0.2);
                Require(debug.DisplayedStep == snapshot.State.StepIndex && debug.ActiveText.Contains("-456"),
                    "overlay uses the completed snapshot");
                var samples = debug.GraphSamples;
                debug.Observe(snapshot, 0.2);
                Require(debug.GraphSamples == samples && samples > 2, "graphs sample each completed state once");
                foreach (var (count, name) in new[] { (2, "None"), (3, "Soft"), (5, "Hard"), (0, "None") })
                {
                    debug.Observe(snapshot with { Timing = snapshot.Timing with { ConsecutiveLateSteps = count } }, 0.2);
                    Require(debug.WarningText.Contains($"Deadline: {name}"), "visible deadline warning thresholds and reset");
                }
                debug.Observe(snapshot with { Fault = new InvalidOperationException("test fault") }, 0.2);
                Require(debug.WarningText.Contains("STOPPED: test fault"), "worker fault visible in F1");
                debug.Visible = false;
                Require(snapshot.State.Input.Controls.Tiller == 0, "focus loss releases and centres tiller");
                _Notification((int)NotificationApplicationFocusIn);
                var beforeMouse = SailingKeyMap.Read();
                var beforeYaw = cameraYaw;
                _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
                _UnhandledInput(new InputEventMouseMotion { Relative = new(20, 10) });
                Require(cameraYaw != beforeYaw, "mouse actually moves the camera");
                Require(SailingKeyMap.Read() == beforeMouse, "camera leaves sailing keys unchanged");
                VerifyPresentation(snapshot.State);
                VerifySessionSwitching();
                runner!.Stop();
                Require(!runner.Latest.Running, "shutdown joins worker");
                GD.Print($"PASS Godot scene: F1 settings/telemetry/graphs/performance, keys, F2 controls help, transforms, focus, render stall, both cameras, sectional sail, foils/body, waves, camera isolation, F1 boat switching, shutdown; steps={runner.Latest.State.StepIndex}");
                GetTree().Quit();
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL Godot smoke: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    // The picker must hold the session until a choice, then start exactly that boat.
    private void CheckPicker()
    {
        try
        {
            if (session is null)
            {
                Require(picker.Visible && !controlsHelp.Visible, "picker shown before sailing");
                var buttons = picker.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
                Require(buttons.Select(b => b.Text).SequenceEqual(BoatCatalog.All.Select(BoatCatalog.Name)), "picker lists both boats");
                buttons.Single(b => b.Text == BoatCatalog.Name(BoatChoice.Hunter235)).EmitSignal(Button.SignalName.Pressed);
                Require(session is { Boat: BoatChoice.Hunter235 } && !picker.Visible && hunterBoat.Visible && !ilcaBoat.Visible,
                    "picking the Hunter starts its session");
                smokeStart = Stopwatch.GetTimestamp();
                return;
            }
            if (Stopwatch.GetElapsedTime(smokeStart).TotalSeconds < 0.5) return;
            var latest = session.Runner.Latest;
            Require(latest.Fault is null && latest.Running && latest.State.StepIndex >= 10 && latest.State.HunterRig is not null,
                "the picked Hunter session steps");
            session.Runner.Stop();
            GD.Print($"PASS Boat picker: no session before choice, Hunter started on selection; steps={latest.State.StepIndex}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL Boat picker: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void VerifySessionSwitching()
    {
        foreach (var choice in new[] { BoatChoice.Hunter235, BoatChoice.Ilca7, BoatChoice.Hunter235 })
        {
            var previous = session!.Runner;
            var retained = environment;
            SendKey(Key.D, true);
            Require(SailingKeyMap.Read().Tiller == 1, "held key before restart");
            debug.Boat.Select(debug.Boat.GetItemIndex((int)choice));
            debug.RestartButton.EmitSignal(Button.SignalName.Pressed);
            Require(!previous.Latest.Running && session.Runner != previous && session.Boat == choice,
                "F1 restart joins the previous worker and selects the boat");
            Require(SailingKeyMap.Read() == HeldSailingInput.Released, "restart releases held keys");
            Require(debug.GraphSamples == 0 && water.Trail.Samples.Count == 0, "restart clears graphs and wake");
            Require(activeBoat.Visible && ilcaBoat.Visible == (choice == BoatChoice.Ilca7) &&
                hunterBoat.Visible == (choice == BoatChoice.Hunter235), "only the selected boat is shown");
            Thread.Sleep(100);
            var latest = session.Runner.Latest;
            Require(latest.Fault is null && latest.Running && latest.State.StepIndex >= 3, "the new worker steps");
            Require(ReferenceEquals(latest.State.Input.Environment, retained) && retained.WaveSeed == -456,
                "wind, waves and seed are retained");
            Require(latest.State.Input.Controls == BoatCatalog.InitialControls(choice) && latest.State.TimeSeconds < 1,
                "time and controls reset for the new boat");
            Render(latest.State);
            var datum = latest.State.Boat.PositionWorldM - latest.State.Boat.OrientationBodyToWorld.Rotate(latest.State.Mass!.CenterOfMassBodyM);
            Require(activeBoat.Position.DistanceTo(SceneFrames.ToGodot(datum)) < 0.0001, "the selected boat renders the new session");
            if (latest.State.HunterGeometry is { } geometry)
                VerifyHunterPresentation(latest.State, geometry);
            debug.Visible = true;
            debug.Observe(latest, 0.2);
            Require(debug.DisplayedStep == latest.State.StepIndex, "F1 displays the new session");
            Require(debug.ForcesText.Contains("Sails (main + jib)") == (choice == BoatChoice.Hunter235) &&
                debug.ForcesText.Contains("Jib sheets") == (choice == BoatChoice.Hunter235), "F1 shows per-sail Hunter data");
            debug.Visible = false;
            SendKey(Key.D, false);
            VerifyBoatControls(choice);
        }
    }

    /// <summary>The Hunter view is drawn from the same completed strips, sheets, foils and skipper as the physics.</summary>
    private void VerifyHunterPresentation(SimulationState state, HunterBoatGeometry geometry)
    {
        var rig = state.HunterRig!;
        Require(view.Sailor.Position.DistanceTo(SceneFrames.ToGodot(geometry.SkipperBodyM)) < 1e-5, "Hunter skipper position");
        Require(hunterBoat.Board.Position.DistanceTo(SceneFrames.ToGodot((geometry.CenterboardTopBodyM + geometry.CenterboardTipBodyM) * 0.5)) < 1e-5,
            "Hunter board position");
        Require(hunterBoat.Rudder.Position.DistanceTo(SceneFrames.ToGodot(geometry.RudderTopBodyM)) < 1e-5, "Hunter rudder pivot");
        Require(hunterBoat.Boom.Position.DistanceTo(SceneFrames.ToGodot((rig.Main.TackBodyM + rig.Main.ClewBodyM) * 0.5)) < 1e-5, "Hunter boom follows the main foot");
        foreach (var (mesh, sail) in new[] { (hunterBoat.MainMesh, rig.Main), (hunterBoat.JibMesh, rig.Jib) })
        {
            var vertices = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var strips = SailingVisuals.HunterStrips(sail);
            // Each cell starts at its lower-left point; row r, column c is cell (r * 10 + c), vertex 6 per cell.
            for (var row = 0; row < strips.Length - 1; row++)
            for (var column = 0; column < 10; column += 3)
                Require(vertices[(row * 10 + column) * 6].DistanceTo(SceneFrames.ToGodot(
                    SailingVisuals.SailPoint(strips[row], column / 10.0, state.TimeSeconds))) < 1e-5, $"{sail.Id} cloth follows the published strips");
            Require(vertices[0].DistanceTo(SceneFrames.ToGodot(sail.TackBodyM)) < 1e-5, $"{sail.Id} tack");
        }
        Require(hunterBoat.TelltaleMesh.GetSurfaceCount() == 1 && hunterBoat.SheetMesh.GetSurfaceCount() == 1, "Hunter telltales and sheets drawn");
        var sheetVertices = hunterBoat.SheetMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Require(sheetVertices[0].DistanceTo(SceneFrames.ToGodot(rig.PortSheet!.Value.LeadBodyM)) < 1e-5 &&
            sheetVertices[15].DistanceTo(SceneFrames.ToGodot(rig.PortSheet.Value.ClewBodyM)) < 1e-5, "jib sheet runs from lead to clew");
        Require(Math.Abs(cameraDistance - hunterBoat.CameraDistance) < 1e-6 && water.WakeStartHalfWidthM == hunterBoat.WakeStartHalfWidthM,
            "Hunter camera distance and wake width fitted");
        onboard = true;
        UpdateCamera();
        Require(camera.Position.DistanceTo(hunterBoat.ToGlobal(hunterBoat.Sailor.Position + hunterBoat.EyeOffset)) < 1e-4 && !hunterBoat.Sailor.Visible,
            "onboard camera follows the Hunter skipper");
        onboard = false;
        UpdateCamera();
    }

    /// <summary>Jib-sheet keys reach only the Hunter, through the same gradual ramps, and F2 lists them only there.</summary>
    private void VerifyBoatControls(BoatChoice choice)
    {
        var hunter = choice == BoatChoice.Hunter235;
        var rows = controlsHelp.FindChild("Rows", owned: false).GetChildren().OfType<Label>().Select(label => label.Text).ToArray();
        Require(rows.Contains("O / L") == hunter && rows.Contains("P / ;") == hunter && rows.Contains("W / S"),
            "F2 lists the selected boat's controls");
        Require(rows.Length == 2 * ControlsHelp.Rows(hunter, "").Count(), "F2 rows are complete");
        var before = runner!.Latest.State.Input.Controls;
        SendKey(Key.L, true);
        SendKey(Key.P, true);
        Require(SailingKeyMap.Read(hunter) == new HeldSailingInput(portJibSheet: hunter ? -1 : 0, starboardJibSheet: hunter ? 1 : 0),
            "jib-sheet keys apply only to the Hunter");
        var start = runner.Latest.State.StepIndex;
        while (runner.Latest.State.StepIndex < start + 30)
        {
            runner.PublishInput(new(SailingKeyMap.Read(hunter), environment));
            Thread.Sleep(5);
        }
        var after = runner.Latest.State.Input.Controls;
        Require(hunter
            ? after.StarboardJibSheet > before.StarboardJibSheet && after.StarboardJibSheet - before.StarboardJibSheet < 0.25 &&
              after.PortJibSheet == 0
            : after.StarboardJibSheet == before.StarboardJibSheet && after.PortJibSheet == before.PortJibSheet,
            "held jib-sheet keys ramp gradually on the Hunter");
        _Notification((int)NotificationApplicationFocusOut);
        Require(SailingKeyMap.Read() == HeldSailingInput.Released, "focus loss releases jib-sheet keys");
        _Notification((int)NotificationApplicationFocusIn);
        SendKey(Key.L, false);
        SendKey(Key.P, false);
    }

    private void VerifyDebugControls(HeldSailingInput keys)
    {
        var graph = new DebugGraph("Window check", 1);
        debug.AddChild(graph);
        for (var i = 0; i < 1200; i++) graph.Add(i / 60.0, i);
        Require(graph.SampleCount == 600, "graph capacity is bounded");
        graph.Add(1199 / 60.0, -1);
        Require(graph.SampleCount == 600, "duplicate graph timestamps ignored");
        graph.Add(100, 0);
        Require(graph.SampleCount == 1, "graph time window evicts old samples after a gap");
        graph.QueueFree();
        Require(!debug.Visible && debug.Settings.WindSpeedKnots == 10 && debug.Settings.AlignWaves &&
            debug.Settings.WavePreset == WavePreset.Gentle && debug.Settings.WaveSeed == 123, "F1 defaults");
        SendKey(Key.F1, true);
        Require(debug.Visible, "F1 opens diagnostics");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F1, Pressed = true, Echo = true });
        Input.FlushBufferedEvents();
        Require(debug.Visible, "F1 repeat ignored");
        SendKey(Key.F1, false);
        Require(debug.WindSpeed.MinValue == 0 && debug.WindSpeed.MaxValue == 30 &&
            debug.WindDirection.MaxValue == 360 && !debug.WaveDirection.Editable, "slider ranges and aligned mode");
        debug.WindSpeed.Value = 30;
        debug.WindDirection.Value = 90;
        Require(environment.WaveSeed == 123 && environment.TrueWindVelocityWorldMps.Y < -15 &&
            environment.WaveTravelDirectionWorld.Y < -0.99, "compass wind and aligned waves retain seed");
        debug.WindSpeed.Value = 0;
        debug.WindDirection.Value = 270;
        Require(environment.TrueWindVelocityWorldMps.Length == 0 && environment.WaveTravelDirectionWorld.Y > 0.99,
            "zero wind retains direction");
        debug.Align.ButtonPressed = false;
        Require(debug.WaveDirection.Editable, "independent direction enabled");
        debug.WaveDirection.Value = 180;
        debug.WindSpeed.Value = 10;
        debug.Strength.Select((int)WavePreset.Moderate);
        debug.Strength.EmitSignal(OptionButton.SignalName.ItemSelected, (long)WavePreset.Moderate);
        debug.Seed.GetLineEdit().Text = "-456";
        var buttons = debug.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
        buttons.Single(b => b.Text == "Apply seed").EmitSignal(Button.SignalName.Pressed);
        buttons.Single(b => b.Text == "Save these conditions").EmitSignal(Button.SignalName.Pressed);
        debug.WindSpeed.Value = 5;
        buttons.Single(b => b.Text == "Restore saved conditions").EmitSignal(Button.SignalName.Pressed);
        Require(debug.Settings.WindSpeedKnots == 10 && environment.WaveTravelDirectionWorld.X > 0.99 &&
            environment.WaveSeed == -456, $"seed application and saved conditions: {debug.Settings}, wave {environment.WaveTravelDirectionWorld}");
        SendKey(Key.F1, true);
        SendKey(Key.F1, false);
        Require(!debug.Visible && SailingKeyMap.Read() == keys, "F1 closes and preserves held sailing keys");
    }

    private void VerifyPresentation(SimulationState state)
    {
        Require(ilcaBoat.SailMesh.GetSurfaceCount() == 1, "sectional sail mesh");
        var sailVertices = ilcaBoat.SailMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var first = SailingVisuals.SailPoint(SailingVisuals.EdgeSection(state.Diagnostics!.SailShape, false), 0, state.TimeSeconds);
        Require(sailVertices[0].DistanceTo(SceneFrames.ToGodot(first)) < 1e-5, "sail uses published geometry");
        for (var row = 0; row < state.Diagnostics.SailShape.Sections.Count; row++)
        for (var column = 0; column < 10; column++)
        {
            var expected = SailingVisuals.SailPoint(state.Diagnostics.SailShape.Sections[row], column / 10.0, state.TimeSeconds);
            Require(sailVertices[(row + 1) * 60 + column * 6].DistanceTo(SceneFrames.ToGodot(expected)) < 1e-5,
                "all sail strips use published geometry");
        }
        var chordEnd = ilcaBoat.Boom.ToGlobal(new(0, 0, 1));
        var expectedChord = ilcaBoat.ToGlobal(new((float)Math.Sin(state.Rig.BoomAngleRad), 0.5f,
            -(float)SectionalSailModel.MastXM + (float)Math.Cos(state.Rig.BoomAngleRad)));
        Require(chordEnd.DistanceTo(expectedChord) < 1e-5, "boom side matches aerodynamic chord");
        var mass = new IlcaMassModel().Contributors(state.Input.Controls);
        Require(ilcaBoat.Sailor.Position.DistanceTo(SceneFrames.ToGodot(mass.Single(c => c.Name == "Sailor").CenterBodyM)) < 1e-5, "sailor mass location");
        Require(ilcaBoat.Board.Position.DistanceTo(SceneFrames.ToGodot(mass.Single(c => c.Name == "Centerboard").CenterBodyM)) < 1e-5, "board mass location");
        Require((int)water.Material.GetShaderParameter("mode_count") == state.Input.Environment.Water.Components.Count, "shared wave spectrum");
        Require(Math.Abs((float)water.Material.GetShaderParameter("simulation_time") - state.TimeSeconds) < 1e-4, "shared wave time");
        var keys = SailingKeyMap.Read();
        UpdateCamera();
        var thirdPosition = camera.Position;
        _UnhandledInput(new InputEventKey { PhysicalKeycode = Key.C, Pressed = true });
        UpdateCamera();
        Require(onboard && camera.Position.DistanceTo(thirdPosition) > 1, "C switches camera");
        Require(camera.Position.DistanceTo(ilcaBoat.ToGlobal(ilcaBoat.Sailor.Position + ilcaBoat.EyeOffset)) < 1e-5, "onboard attachment");
        var before = camera.Transform;
        _UnhandledInput(new InputEventMouseMotion { Relative = new(15, 10) });
        _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
        UpdateCamera();
        Require(camera.Transform != before && camera.Fov < 80, "onboard look and zoom");
        Require(SailingKeyMap.Read() == keys, "both camera modes isolate sailing input");
        _Notification((int)NotificationApplicationFocusOut);
        var savedYaw = onboardYaw;
        _UnhandledInput(new InputEventMouseMotion { Relative = new(50, 50) });
        Require(onboardYaw == savedYaw, "focus loss stops onboard look");
        _Notification((int)NotificationApplicationFocusIn);
        _UnhandledInput(new InputEventKey { PhysicalKeycode = Key.C, Pressed = true });
        UpdateCamera();
        Require(!onboard && camera.Position.DistanceTo(thirdPosition) < 1e-5, "third-person orbit retained");
        var stillShape = state.Diagnostics.SailShape with
        {
            Sections = state.Diagnostics.SailShape.Sections.Select(s => s with { ApparentWindBodyMps = default }).ToArray()
        };
        ilcaBoat.RenderSail(stillShape, state.TimeSeconds);
        var drooping = ilcaBoat.TelltaleMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var i = 0; i < drooping.Length; i += 3)
            Require((drooping[i + 1] - drooping[i]).Cross(drooping[i + 2] - drooping[i]).LengthSquared() > 1e-10,
                "still-air telltales have visible area");
        ilcaBoat.RenderSail(state.Diagnostics.SailShape, state.TimeSeconds);
    }

    private static void VerifyKeyBindings()
    {
        var pairs = new (Key Positive, Key Negative, HeldSailingInput Expected)[]
        {
            (Key.D, Key.A, new(tiller: 1)), (Key.W, Key.S, new(mainsheet: 1)),
            (Key.Q, Key.E, new(traveler: 1)), (Key.R, Key.F, new(vang: 1)),
            (Key.T, Key.G, new(outhaul: 1)), (Key.U, Key.J, new(cunningham: 1)),
            (Key.I, Key.K, new(centerboard: 1)), (Key.X, Key.Z, new(hiking: 1)),
            (Key.O, Key.L, new(portJibSheet: 1)), (Key.P, Key.Semicolon, new(starboardJibSheet: 1))
        };
        foreach (var (positive, negative, expected) in pairs)
        {
            SendKey(positive, true);
            Require(SailingKeyMap.Read() == expected, $"positive binding {positive}");
            SendKey(negative, true);
            Require(SailingKeyMap.Read() == HeldSailingInput.Released, "opposed keys cancel");
            SendKey(positive, false);
            Require(SailingKeyMap.Read() == new HeldSailingInput(
                -expected.Tiller, -expected.Mainsheet, -expected.Traveler, -expected.Vang,
                -expected.Outhaul, -expected.Cunningham, -expected.Centerboard, -expected.Hiking,
                -expected.PortJibSheet, -expected.StarboardJibSheet), $"negative binding {negative}");
            SendKey(negative, false);
        }
    }

    private static void SendKey(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = pressed });
        Input.FlushBufferedEvents();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
