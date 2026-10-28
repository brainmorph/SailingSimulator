using Godot;
using SailingSimulator.Core;

/// <summary>A rendered boat driven only by completed snapshots.</summary>
public interface IBoatView
{
    /// <summary>The crew marker; onboard cameras attach relative to it.</summary>
    Node3D Sailor { get; }
    void Render(SimulationState state);
}

/// <summary>Provisional Hunter geometry from published hull, foil, skipper and sail state.</summary>
public partial class HunterPresentation : Node3D, IBoatView
{
    public ImmediateMesh MainMesh { get; } = new();
    public ImmediateMesh JibMesh { get; } = new();
    public MeshInstance3D Board { get; private set; } = null!;
    public MeshInstance3D Rudder { get; private set; } = null!;
    public Node3D Sailor { get; private set; } = null!;

    public void Build(HunterBoatGeometry geometry)
    {
        BoatPresentation.AddMesh(this, new BoxMesh { Size = new((float)geometry.HullBeamM, (float)geometry.HullHeightM, (float)geometry.HullLengthM) },
            Vector3.Zero, new(0.85f, 0.87f, 0.82f));
        BoatPresentation.AddMesh(this, MainMesh, Vector3.Zero, new(0.98f, 0.94f, 0.8f));
        BoatPresentation.AddMesh(this, JibMesh, Vector3.Zero, new(0.88f, 0.91f, 0.82f));
        Board = BoatPresentation.AddMesh(this, new BoxMesh { Size = new(0.06f, 1, 0.55f) }, Vector3.Zero, new(0.45f, 0.35f, 0.2f));
        Rudder = BoatPresentation.AddMesh(this, new BoxMesh { Size = new(0.06f, 1, 0.4f) }, Vector3.Zero, new(0.45f, 0.35f, 0.2f));
        Sailor = new Node3D { Name = "Skipper" };
        AddChild(Sailor);
        BoatPresentation.AddMesh(Sailor, new SphereMesh { Radius = 0.2f, Height = 0.4f }, Vector3.Zero, new(0.9f, 0.25f, 0.1f));
    }

    public void Render(SimulationState state)
    {
        var geometry = state.HunterGeometry!;
        var rig = state.HunterRig!;
        var datum = state.Boat.PositionWorldM - state.Boat.OrientationBodyToWorld.Rotate(state.Mass!.CenterOfMassBodyM);
        Transform = new(new Basis(SceneFrames.ToGodot(state.Boat.OrientationBodyToWorld)), SceneFrames.ToGodot(datum));
        SetFoil(Board, geometry.CenterboardTopBodyM, geometry.CenterboardTipBodyM);
        SetFoil(Rudder, geometry.RudderTopBodyM, geometry.RudderTipBodyM);
        Sailor.Position = SceneFrames.ToGodot(geometry.SkipperBodyM);
        Sail(MainMesh, rig.Main);
        Sail(JibMesh, rig.Jib);
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
}
