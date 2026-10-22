using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using System.Diagnostics;

/// <summary>Only this main-thread adapter reads input or touches Godot nodes.</summary>
public partial class SailingScene : Node3D
{
    private SimulationRunner? runner;
    private Node3D boat = null!;
    private Node3D boom = null!;
    private MeshInstance3D water = null!;
    private Camera3D camera = null!;
    private Label status = null!;
    private EnvironmentConfig environment = null!;
    private bool focused = true;
    private bool orbiting;
    private float cameraYaw = 0.6f;
    private float cameraPitch = 0.35f;
    private float cameraDistance = 10;
    private bool smokeTest;
    private bool stalled;
    private bool released;
    private long smokeStart;
    private long previousRenderedStep;

    public override void _Ready()
    {
        SailingKeyMap.Register();
        smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
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
        environment = PresentationConversions.EnvironmentFromSettings(waveSeed: smokeTest ? 123 : null);
        var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, hiking: -0.3);
        var input = new SimulationInput(controls, environment);
        var model = new IlcaBoatModel();
        // Start on a beam reach in the default northerly wind.
        var simulation = new Simulation(model.CreateInitialState(input, Math.PI / 2), new IlcaMassModel(), model, input);
        runner = new(simulation);
        Render(simulation.Latest);
        runner.Start();
        smokeStart = Stopwatch.GetTimestamp();
        if (smokeTest)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
        }
    }

    public override void _Process(double delta)
    {
        if (runner is null) return;
        runner.PublishInput(new(focused ? SailingKeyMap.Read() : HeldSailingInput.Released, environment));
        var snapshot = runner.Latest; // One completed snapshot drives the whole render frame.
        Render(snapshot.State);
        status.Text = snapshot.Fault is not null ? $"Simulation stopped: {snapshot.Fault.Message}"
            : snapshot.Timing.Warning == DeadlineWarning.None ? "" : $"Simulation deadline: {snapshot.Timing.Warning}";
        if (smokeTest) SmokeCheck(snapshot);
    }

    private void Render(SimulationState state)
    {
        // Simulation position is combined COM; meshes are authored around the hull datum.
        var hullDatum = state.Boat.PositionWorldM - state.Boat.OrientationBodyToWorld.Rotate(state.Mass!.CenterOfMassBodyM);
        boat.Transform = new(new Basis(ToGodot(state.Boat.OrientationBodyToWorld)), ToGodot(hullDatum));
        boom.Rotation = new(0, -(float)state.Rig.BoomAngleRad, 0);
        water.Position = new(boat.Position.X, 0, boat.Position.Z);
        var target = boat.Position + Vector3.Up * 0.8f;
        camera.Position = target + new Vector3(Mathf.Sin(cameraYaw) * Mathf.Cos(cameraPitch),
            Mathf.Sin(cameraPitch), Mathf.Cos(cameraYaw) * Mathf.Cos(cameraPitch)) * cameraDistance;
        camera.LookAt(target);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right) orbiting = button.Pressed && focused;
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp) cameraDistance = Mathf.Max(4, cameraDistance - 1);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown) cameraDistance = Mathf.Min(30, cameraDistance + 1);
        }
        if (@event is InputEventMouseMotion motion && orbiting)
        {
            cameraYaw -= motion.Relative.X * 0.005f;
            cameraPitch = Mathf.Clamp(cameraPitch + motion.Relative.Y * 0.005f, 0.08f, 1.35f);
        }
    }

    public override void _Notification(int what)
    {
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
        runner?.Dispose(); // Wake the deadline wait and join before nodes disappear.
        runner = null;
    }

    private void BuildScene()
    {
        var light = new DirectionalLight3D { RotationDegrees = new(-45, -35, 0), LightEnergy = 1.2f };
        AddChild(light);
        boat = new Node3D { Name = "Boat" };
        AddChild(boat);
        AddMesh(boat, new BoxMesh { Size = new(1.37f, 0.25f, 4.23f) }, new(0, 0, 0), new(0.88f, 0.9f, 0.86f));
        AddMesh(boat, new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 5.7f },
            new(0, 2.85f, -0.55f), new(0.24f, 0.27f, 0.29f));
        boom = new Node3D { Name = "Boom", Position = new(0, 0.65f, -0.55f) };
        boat.AddChild(boom);
        AddMesh(boom, new BoxMesh { Size = new(0.06f, 0.06f, 2.5f) }, new(0, 0, 1.25f), new(0.24f, 0.27f, 0.29f));
        // A simple rig proxy for integration; sectional sail feedback belongs to Phase 6.
        var sail = new ImmediateMesh();
        sail.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        sail.SurfaceAddVertex(new(0, 0, 0));
        sail.SurfaceAddVertex(new(0, 4.9f, 0));
        sail.SurfaceAddVertex(new(0, 0, 2.5f));
        sail.SurfaceEnd();
        AddMesh(boom, sail, Vector3.Zero, new(0.96f, 0.95f, 0.85f));
        water = AddMesh(this, new PlaneMesh { Size = new(2000, 2000) }, Vector3.Zero, new(0.08f, 0.3f, 0.41f));
        camera = new Camera3D { Current = true, Far = 1500 };
        AddChild(camera);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        status = new Label { Position = new(16, 16) };
        canvas.AddChild(status);
    }

    private static MeshInstance3D AddMesh(Node parent, Mesh mesh, Vector3 position, Color color)
    {
        var node = new MeshInstance3D
        {
            Mesh = mesh, Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, CullMode = BaseMaterial3D.CullModeEnum.Disabled }
        };
        parent.AddChild(node);
        return node;
    }

    private static Vector3 ToGodot(Vector3d value)
    {
        var v = PresentationConversions.WorldToGodot(value);
        return new((float)v.X, (float)v.Y, (float)v.Z);
    }

    private static Quaternion ToGodot(Quaterniond value)
    {
        var q = PresentationConversions.BodyToWorldToGodot(value);
        return new((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }

    private void SmokeCheck(RunnerSnapshot snapshot)
    {
        try
        {
            Require(snapshot.Fault is null, "worker fault");
            Require(snapshot.State.StepIndex >= previousRenderedStep, "snapshot order");
            previousRenderedStep = snapshot.State.StepIndex;
            var datum = snapshot.State.Boat.PositionWorldM - snapshot.State.Boat.OrientationBodyToWorld.Rotate(snapshot.State.Mass!.CenterOfMassBodyM);
            Require(boat.Position.DistanceTo(ToGodot(datum)) < 0.0001, "rendered hull datum");
            Require(Math.Abs(boat.Quaternion.Dot(ToGodot(snapshot.State.Boat.OrientationBodyToWorld))) > 0.9999, "rendered orientation");
            var elapsed = Stopwatch.GetElapsedTime(smokeStart).TotalSeconds;
            if (!stalled && elapsed > 0.4)
            {
                Require(snapshot.State.Input.Controls.Tiller > 0.1 && snapshot.State.Input.Controls.Mainsheet > 0.6, "held physical key input");
                var step = runner!.Latest.State.StepIndex;
                Thread.Sleep(250); // Deliberate main-thread stall: the worker must keep stepping.
                Require(runner.Latest.State.StepIndex >= step + 10, "render-independent stepping");
                stalled = true;
                _Notification((int)NotificationApplicationFocusOut);
                released = true;
            }
            if (released && elapsed > 1.5)
            {
                Require(snapshot.State.Input.Controls.Tiller == 0, "focus loss releases and centres tiller");
                _Notification((int)NotificationApplicationFocusIn);
                var beforeMouse = SailingKeyMap.Read();
                var beforeYaw = cameraYaw;
                _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
                _UnhandledInput(new InputEventMouseMotion { Relative = new(20, 10) });
                Require(cameraYaw != beforeYaw, "mouse actually moves the camera");
                Require(SailingKeyMap.Read() == beforeMouse, "camera leaves sailing keys unchanged");
                runner!.Stop();
                Require(!runner.Latest.Running, "shutdown joins worker");
                GD.Print($"PASS Godot scene: keys, transforms, focus, render stall, camera isolation, shutdown; steps={runner.Latest.State.StepIndex}");
                GetTree().Quit();
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL Godot smoke: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyKeyBindings()
    {
        var pairs = new (Key Positive, Key Negative, HeldSailingInput Expected)[]
        {
            (Key.D, Key.A, new(tiller: 1)), (Key.W, Key.S, new(mainsheet: 1)),
            (Key.Q, Key.E, new(traveler: 1)), (Key.R, Key.F, new(vang: 1)),
            (Key.T, Key.G, new(outhaul: 1)), (Key.U, Key.J, new(cunningham: 1)),
            (Key.I, Key.K, new(centerboard: 1)), (Key.X, Key.Z, new(hiking: 1))
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
                -expected.Outhaul, -expected.Cunningham, -expected.Centerboard, -expected.Hiking), $"negative binding {negative}");
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
