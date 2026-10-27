using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;
using System.Diagnostics;

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
    private long start;
    private long lastStep = -1;

    public override void _Ready()
    {
        smoke = OS.GetCmdlineUserArgs().Contains("--hunter-smoke");
        AddChild(new DirectionalLight3D { RotationDegrees = new(-45, -35, 0), LightEnergy = 1.2f });
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new(0.7f, 0.8f, 0.9f), AmbientLightEnergy = 0.8f
        } });
        boat = new Node3D();
        AddChild(boat);
        var environment = PresentationConversions.EnvironmentFromSettings(wavePreset: WavePreset.Calm, waveSeed: 123);
        var controls = new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
            outhaul: 0.5, cunningham: 0.3, portJibSheet: 0.65);
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
        var snapshot = runner.Latest;
        if (snapshot.Fault is not null)
        {
            GD.PushError($"Hunter worker stopped: {snapshot.Fault}");
            if (smoke) GetTree().Quit(1);
            return;
        }
        if (snapshot.State.StepIndex != lastStep) Render(snapshot.State);
        var rig = snapshot.State.HunterRig!;
        var controls = snapshot.State.Input.Controls;
        telemetry.Text = $"HUNTER DEVELOPMENT · provisional physics\n" +
            $"step {snapshot.State.StepIndex}  time {snapshot.State.TimeSeconds:F2} s  worker {snapshot.Timing.StepMilliseconds:F2} ms  late {snapshot.Timing.TotalLateSteps}\n" +
            $"main {rig.Main.Force.ForceWorldN.Length:F1} N  jib {rig.Jib.Force.ForceWorldN.Length:F1} N  total sail {rig.Total.ForceWorldN.Length:F1} N\n" +
            $"jib sheets port {controls.PortJibSheet:F2}  starboard {controls.StarboardJibSheet:F2}  board {controls.Centerboard:F2}  crew {controls.Hiking:F2}\n" +
            $"frame {delta * 1000:F1} ms · fixed development inputs";
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
            GD.Print($"PASS Hunter development scene: worker snapshots, hull, sails, foils, skipper and telemetry; steps={snapshot.State.StepIndex}");
            GetTree().Quit();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.F1)
            telemetry.Visible = !telemetry.Visible;
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
