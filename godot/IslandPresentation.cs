using Godot;
using SailingSimulator.Core;

/// <summary>
/// Static visual landmarks so boat motion is readable against the water. They have no physics:
/// the boat sails through them, and they do not affect wind or waves.
/// </summary>
public partial class IslandPresentation : Node3D
{
    /// <summary>Fixed layout in world north/east metres around the origin start, with base radius.</summary>
    internal static readonly (double North, double East, float Radius)[] Layout =
    [
        (-90, 180, 14), (150, 420, 30), (-260, 650, 45), (380, 90, 25), (-420, -150, 35),
        (250, -380, 40), (-60, -260, 20), (620, 520, 60), (-700, 350, 55), (520, -650, 50),
        (40, 900, 70), (-850, -600, 80), (900, -100, 65),
    ];

    private static readonly Color Sand = new(0.86f, 0.78f, 0.58f);
    private static readonly Color Grass = new(0.28f, 0.52f, 0.25f);
    private static readonly Color Rock = new(0.45f, 0.43f, 0.40f);
    private static readonly Color Trunk = new(0.45f, 0.32f, 0.2f);
    private static readonly Color Leaves = new(0.18f, 0.45f, 0.2f);

    public void Build()
    {
        for (var i = 0; i < Layout.Length; i++)
        {
            var (north, east, radius) = Layout[i];
            var island = new Node3D { Name = $"Island{i}", Position = SceneFrames.ToGodot(new Vector3d(north, east, 0)) };
            AddChild(island);
            // Beach shelf reaches below the deepest wave troughs so no gap opens at the waterline.
            Add(island, new CylinderMesh { TopRadius = radius * 0.9f, BottomRadius = radius * 1.15f, Height = 5 },
                new(0, -1.5f, 0), Sand);
            var hill = radius * (0.35f + 0.1f * (i % 3));
            var hillRadius = radius * 0.7f;
            Add(island, new SphereMesh { Radius = hillRadius, Height = hill * 2, RadialSegments = 24, Rings = 12 },
                new(radius * 0.1f, 0.8f, 0), i % 4 == 3 ? Rock : Grass);
            if (i % 2 == 0)
            {
                // A palm on the beach gives a vertical cue against the horizon.
                var palm = new Vector3(-radius * 0.55f, 1, radius * 0.45f);
                Add(island, new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.4f, Height = 9 }, palm + new Vector3(0, 4.5f, 0), Trunk);
                Add(island, new SphereMesh { Radius = 3, Height = 2.4f }, palm + new Vector3(0, 9.2f, 0), Leaves);
            }
        }
    }

    private static void Add(Node3D parent, PrimitiveMesh mesh, Vector3 position, Color color) =>
        parent.AddChild(new MeshInstance3D
        {
            Mesh = mesh, Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f }
        });
}
