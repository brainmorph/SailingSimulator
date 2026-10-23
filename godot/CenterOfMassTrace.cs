using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

/// <summary>Red world-space line through recent center-of-mass positions, drawn over the hull.</summary>
public partial class CenterOfMassTrace : Node3D
{
    public CenterOfMassTrail Trail { get; } = new();
    private readonly ImmediateMesh line = new();
    internal int VertexCount { get; private set; }

    public void Build()
    {
        var node = BoatPresentation.AddMesh(this, line, Vector3.Zero, new(1, 0.1f, 0.1f), unshaded: true);
        node.TopLevel = true;
        // The center of mass lies inside the hull; skip depth testing so the whole trace stays visible.
        ((StandardMaterial3D)node.MaterialOverride).NoDepthTest = true;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public void Render(SimulationState state)
    {
        Trail.Update(state); // Records while hidden so enabling the trace shows recent history.
        line.ClearSurfaces();
        VertexCount = 0;
        if (!Visible || Trail.Samples.Count < 2) return;
        line.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var sample in Trail.Samples) line.SurfaceAddVertex(SceneFrames.ToGodot(sample.PositionWorldM));
        line.SurfaceAddVertex(SceneFrames.ToGodot(state.Boat.PositionWorldM)); // Join to the live position.
        line.SurfaceEnd();
        VertexCount = Trail.Samples.Count + 1;
    }
}
