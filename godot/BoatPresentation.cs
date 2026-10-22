using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

/// <summary>Lightweight meshes authored at the core hull datum. No sailing dynamics live here.</summary>
public partial class BoatPresentation : Node3D
{
    public Node3D Boom { get; private set; } = null!;
    public Node3D Rudder { get; private set; } = null!;
    public MeshInstance3D Board { get; private set; } = null!;
    public Node3D Sailor { get; private set; } = null!;
    public ImmediateMesh SailMesh { get; } = new();
    public ImmediateMesh TelltaleMesh => ribbons;
    private readonly ImmediateMesh seams = new();
    private readonly ImmediateMesh ribbons = new();
    private static readonly Color Port = new(0.95f, 0.12f, 0.08f);
    private static readonly Color Starboard = new(0.08f, 0.7f, 0.22f);

    public void Build()
    {
        var hull = new ImmediateMesh();
        hull.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < 24; i++)
        for (var j = 0; j < 8; j++)
        {
            Vector3 Point(int station, int around)
            {
                var x = -2.115 + 4.23 * station / 24;
                var beam = 0.685 * Math.Sqrt(Math.Max(0, 1 - Math.Pow(x / 2.115, 2)));
                var angle = 2 * Math.PI * around / 8;
                return SceneFrames.ToGodot(new Vector3d(x, beam * Math.Cos(angle), -0.04 + 0.16 * Math.Sin(angle)));
            }
            Triangle(hull, Point(i, j), Point(i + 1, j), Point(i + 1, j + 1));
            Triangle(hull, Point(i, j), Point(i + 1, j + 1), Point(i, j + 1));
        }
        hull.SurfaceEnd();
        AddMesh(this, hull, Vector3.Zero, new(0.9f, 0.91f, 0.86f));
        AddMesh(this, new BoxMesh { Size = new(0.64f, 0.018f, 1.25f) }, new(0, 0.205f, 0.55f), new(0.16f, 0.22f, 0.24f));
        AddMesh(this, new CylinderMesh { TopRadius = 0.027f, BottomRadius = 0.045f, Height = 5.7f },
            new(0, 2.85f, -(float)SectionalSailModel.MastXM), new(0.25f, 0.29f, 0.31f));
        Boom = new Node3D { Name = "Boom", Position = new(0, 0.5f, -(float)SectionalSailModel.MastXM) };
        AddChild(Boom);
        AddMesh(Boom, new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.028f, Height = 2.72f },
            new(0, 0, 1.36f), new(0.3f, 0.33f, 0.34f)).RotationDegrees = new(90, 0, 0);
        var cloth = AddMesh(this, SailMesh, Vector3.Zero, new(0.98f, 0.95f, 0.82f));
        ((StandardMaterial3D)cloth.MaterialOverride).ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
        AddMesh(this, seams, Vector3.Zero, new(0.45f, 0.44f, 0.37f), unshaded: true);
        var ribbonNode = AddMesh(this, ribbons, Vector3.Zero, Colors.White, unshaded: true);
        ((StandardMaterial3D)ribbonNode.MaterialOverride).VertexColorUseAsAlbedo = true;

        Board = AddMesh(this, new BoxMesh { Size = new(0.025f, 0.9f, 0.32f) }, new(0, -0.35f, -0.05f), new(0.77f, 0.63f, 0.37f));
        Rudder = new Node3D { Name = "Rudder", Position = new(0, -0.08f, 2.05f) };
        AddChild(Rudder);
        AddMesh(Rudder, new BoxMesh { Size = new(0.026f, 0.48f, 0.208f) }, new(0, -0.24f, 0), new(0.77f, 0.63f, 0.37f));
        AddMesh(Rudder, new BoxMesh { Size = new(0.035f, 0.035f, 1.05f) }, new(0, 0.36f, -0.5f), new(0.24f, 0.27f, 0.28f));
        Sailor = new Node3D { Name = "Sailor" };
        AddChild(Sailor);
        // Symmetric stylized body around the mass point; its centre follows the exact published contributor.
        AddMesh(Sailor, new CapsuleMesh { Radius = 0.15f, Height = 0.48f }, Vector3.Zero, new(0.92f, 0.3f, 0.1f));
        AddMesh(Sailor, new SphereMesh { Radius = 0.115f, Height = 0.23f }, new(0, 0.34f, 0), new(0.9f, 0.73f, 0.54f));
        AddMesh(Sailor, new BoxMesh { Size = new(0.24f, 0.12f, 0.48f) }, new(0, -0.16f, -0.14f), new(0.12f, 0.17f, 0.2f));
    }

    public void Render(SimulationState state)
    {
        var datum = state.Boat.PositionWorldM - state.Boat.OrientationBodyToWorld.Rotate(state.Mass!.CenterOfMassBodyM);
        Transform = new(new Basis(SceneFrames.ToGodot(state.Boat.OrientationBodyToWorld)), SceneFrames.ToGodot(datum));
        // Chord = (-cos(angle), +sin(angle), 0) in core axes -> (+sin, 0, +cos) in Godot.
        Boom.Rotation = new(0, (float)state.Rig.BoomAngleRad, 0);
        Rudder.Rotation = new(0, (float)(0.45 * state.Input.Controls.Tiller), 0);
        var contributors = new IlcaMassModel().Contributors(state.Input.Controls);
        Sailor.Position = SceneFrames.ToGodot(contributors.Single(c => c.Name == "Sailor").CenterBodyM);
        Board.Position = SceneFrames.ToGodot(contributors.Single(c => c.Name == "Centerboard").CenterBodyM);
        RenderSail(state.Diagnostics!.SailShape, state.TimeSeconds);
    }

    public void RenderSail(SailShapeState shape, double timeSeconds)
    {
        var rows = new[] { SailingVisuals.EdgeSection(shape, false) }.Concat(shape.Sections)
            .Append(SailingVisuals.EdgeSection(shape, true)).ToArray();
        SailMesh.ClearSurfaces();
        seams.ClearSurfaces();
        ribbons.ClearSurfaces();
        SailMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (var row = 0; row < rows.Length - 1; row++)
        for (var column = 0; column < 10; column++)
        {
            Vector3 Point(int r, int c) => SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[r], c / 10.0, timeSeconds));
            Triangle(SailMesh, Point(row, column), Point(row + 1, column), Point(row + 1, column + 1));
            Triangle(SailMesh, Point(row, column), Point(row + 1, column + 1), Point(row, column + 1));
        }
        SailMesh.SurfaceEnd();
        seams.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var section in shape.Sections)
        for (var i = 0; i < 10; i++)
        {
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(section, i / 10.0, timeSeconds)));
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(section, (i + 1) / 10.0, timeSeconds)));
        }
        // Leech makes the section twist readable from either side.
        for (var i = 0; i < rows.Length - 1; i++)
        {
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[i], 1, timeSeconds)));
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[i + 1], 1, timeSeconds)));
        }
        seams.SurfaceEnd();
        ribbons.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var index in new[] { 1, 3, 4 })
        foreach (var side in new[] { -1, 1 })
        for (var i = 0; i < 6; i++)
        {
            ribbons.SurfaceSetColor(side < 0 ? Port : Starboard);
            var a = SceneFrames.ToGodot(SailingVisuals.TelltalePoint(shape.Sections[index], side, i / 6.0, timeSeconds));
            var b = SceneFrames.ToGodot(SailingVisuals.TelltalePoint(shape.Sections[index], side, (i + 1) / 6.0, timeSeconds));
            var section = shape.Sections[index];
            var normal = SceneFrames.ToGodot(new Vector3d(Math.Sin(section.AngleRad), Math.Cos(section.AngleRad), 0));
            var transverse = (b - a).Cross(normal);
            var width = (transverse.LengthSquared() > 1e-10f ? transverse.Normalized() : Vector3.Up) * 0.009f;
            Triangle(ribbons, a - width, b - width, b + width);
            Triangle(ribbons, a - width, b + width, a + width);
        }
        ribbons.SurfaceEnd();
    }

    public static MeshInstance3D AddMesh(Node parent, Mesh mesh, Vector3 position, Color color, bool unshaded = false)
    {
        var node = new MeshInstance3D
        {
            Mesh = mesh, Position = position,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 0.85f,
                ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel
            }
        };
        parent.AddChild(node);
        return node;
    }

    public static void Triangle(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = (b - a).Cross(c - a);
        mesh.SurfaceSetNormal(cross.LengthSquared() > 1e-12f ? cross.Normalized() : Vector3.Up);
        mesh.SurfaceAddVertex(a);
        mesh.SurfaceAddVertex(b);
        mesh.SurfaceAddVertex(c);
    }
}

internal static class SceneFrames
{
    public static Vector3 ToGodot(Vector3d value)
    {
        var v = PresentationConversions.WorldToGodot(value);
        return new((float)v.X, (float)v.Y, (float)v.Z);
    }

    public static Quaternion ToGodot(Quaterniond value)
    {
        var q = PresentationConversions.BodyToWorldToGodot(value);
        return new((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
