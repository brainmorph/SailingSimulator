using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

public partial class WaterPresentation : Node3D
{
    public ShaderMaterial Material { get; private set; } = null!;
    public WakeTrail Trail { get; } = new();
    private readonly ImmediateMesh wake = new();
    private EnvironmentConfig? previousEnvironment;

    public void Build()
    {
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Water.gdshader") };
        // Fine central grid, sparse horizon. Both use the identical spectral shader in world coordinates.
        const int count = 128;
        var vertices = new Vector3[(count + 1) * (count + 1)];
        var normals = new Vector3[vertices.Length];
        var indices = new int[count * count * 6];
        float Coordinate(int i)
        {
            var t = (i - count / 2.0f) / (count / 2);
            return 850 * Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), 3);
        }
        for (var x = 0; x <= count; x++)
        for (var z = 0; z <= count; z++)
        {
            vertices[x * (count + 1) + z] = new(Coordinate(x), 0, Coordinate(z));
            normals[x * (count + 1) + z] = Vector3.Up;
        }
        var cursor = 0;
        for (var x = 0; x < count; x++)
        for (var z = 0; z < count; z++)
        {
            var a = x * (count + 1) + z;
            var b = (x + 1) * (count + 1) + z;
            var c = b + 1;
            var d = a + 1;
            indices[cursor++] = a; indices[cursor++] = c; indices[cursor++] = b;
            indices[cursor++] = a; indices[cursor++] = d; indices[cursor++] = c;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        AddChild(new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = Material,
            CustomAabb = new(new(-850, -2, -850), new(1700, 4, 1700))
        });
        var foam = BoatPresentation.AddMesh(this, wake, Vector3.Zero, Colors.White, unshaded: true);
        foam.TopLevel = true;
        var foamMaterial = (StandardMaterial3D)foam.MaterialOverride;
        foamMaterial.VertexColorUseAsAlbedo = true;
        foamMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
    }

    public void Render(SimulationState state, Vector3 boatPosition)
    {
        // Quantized recentering avoids a moving grid under the hull; shader phase uses world coordinates.
        Position = new(Mathf.Floor(boatPosition.X / 8) * 8, 0, Mathf.Floor(boatPosition.Z / 8) * 8);
        var env = state.Input.Environment;
        if (!ReferenceEquals(env, previousEnvironment))
        {
            var modes = new Vector4[24];
            var phases = new Vector2[24];
            for (var i = 0; i < env.Water.Components.Count; i++)
            {
                var m = SailingVisuals.WaterMode(env.Water.Components[i]);
                modes[i] = new((float)m.DirectionX, (float)m.DirectionZ, (float)m.AmplitudeM, (float)m.Wavenumber);
                phases[i] = new((float)m.Frequency, (float)m.Phase);
            }
            Material.SetShaderParameter("mode_count", env.Water.Components.Count);
            Material.SetShaderParameter("modes", modes);
            Material.SetShaderParameter("phases", phases);
            previousEnvironment = env;
        }
        Material.SetShaderParameter("simulation_time", (float)state.TimeSeconds);
        Trail.Update(state);
        wake.ClearSurfaces();
        if (Trail.Samples.Count == 0) return;
        wake.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var sample in Trail.Samples)
        {
            var age = state.TimeSeconds - sample.TimeSeconds;
            var fade = (float)((1 - age / WakeTrail.LifetimeSeconds) * sample.Strength * 0.5);
            wake.SurfaceSetColor(new(0.8f, 0.94f, 0.95f, fade));
            foreach (var side in new[] { -1, 1 })
            {
                var center = sample.PositionWorldM + sample.SideWorld * (side * (0.32 + age * 0.26));
                var across = sample.SideWorld * (0.04 + age * 0.018);
                var along = new Vector3d(sample.SideWorld.Y, -sample.SideWorld.X, 0) * 0.10;
                Vector3 Surface(Vector3d p) => SceneFrames.ToGodot(p with
                {
                    Z = env.Water.SampleSurface(p, state.TimeSeconds).PositionWorldZM - 0.016
                });
                BoatPresentation.Triangle(wake, Surface(center - across - along), Surface(center + across - along), Surface(center + across + along));
                BoatPresentation.Triangle(wake, Surface(center - across - along), Surface(center + across + along), Surface(center - across + along));
            }
        }
        wake.SurfaceEnd();
    }
}
