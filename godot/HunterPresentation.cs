using Godot;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

/// <summary>A rendered boat driven only by completed snapshots.</summary>
public interface IBoatView
{
    /// <summary>The crew marker; onboard cameras attach relative to it.</summary>
    Node3D Sailor { get; }
    /// <summary>Onboard eye position relative to the crew marker, in boat axes.</summary>
    Vector3 EyeOffset { get; }
    /// <summary>Third-person target height above the hull datum and initial distance.</summary>
    float CameraTargetHeight { get; }
    float CameraDistance { get; }
    /// <summary>Wake half-width at the stern: the stern half-beam plus a small spray margin.</summary>
    double WakeStartHalfWidthM { get; }
    void Render(SimulationState state);
}

/// <summary>
/// Modest-detail Hunter geometry. Hull lines are presentation estimates around the published length,
/// beam and column heights; rig, foils, skipper, sails and sheets come from each completed snapshot.
/// </summary>
public partial class HunterPresentation : Node3D, IBoatView
{
    // Presentation estimates in hull-datum NED (X forward, Y starboard, Z down from the nominal waterline).
    private const double SheerZ = -0.47;
    private const double CockpitAftX = -3.05, CockpitForwardX = -1.0, CockpitHalfWidth = 0.72;
    private const double SeatTopZ = -0.40, CockpitSoleZ = -0.15, CabinForwardX = 1.75, CabinTopZ = -0.62;
    private static readonly Color Port = new(0.95f, 0.12f, 0.08f);
    private static readonly Color Starboard = new(0.08f, 0.7f, 0.22f);
    private readonly ImmediateMesh seams = new();
    private readonly ImmediateMesh ribbons = new();
    private readonly ImmediateMesh sheets = new();

    public ImmediateMesh MainMesh { get; } = new();
    public ImmediateMesh JibMesh { get; } = new();
    public ImmediateMesh TelltaleMesh => ribbons;
    public ImmediateMesh SheetMesh => sheets;
    public MeshInstance3D Board { get; private set; } = null!;
    public Node3D Rudder { get; private set; } = null!;
    public MeshInstance3D Boom { get; private set; } = null!;
    public Node3D Sailor { get; private set; } = null!;
    public Vector3 EyeOffset => new(0, 0.55f, 0.1f);
    public float CameraTargetHeight => 3.4f;
    public float CameraDistance => 14;
    public double WakeStartHalfWidthM => HalfBeam(-Hunter235Geometry.HullLengthM / 2, Hunter235Geometry.HullBeamM) + 0.1;

    public void Build(HunterBoatGeometry geometry)
    {
        var length = geometry.HullLengthM;
        var halfLength = length / 2;
        var hull = new ImmediateMesh();
        hull.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        const int stations = 28, around = 12;
        Vector3d HullPoint(int station, int index)
        {
            var x = -halfLength + length * station / stations;
            var phi = Math.PI * index / around;
            var y = HalfBeam(x, geometry.HullBeamM) * Math.Sign(-Math.Cos(phi)) * Math.Sqrt(Math.Abs(Math.Cos(phi)));
            return new(x, y, Sheer(x) + (Keel(x) - Sheer(x)) * Math.Sin(phi));
        }
        for (var i = 0; i < stations; i++)
        for (var j = 0; j < around; j++)
            Quad(hull, HullPoint(i, j), HullPoint(i + 1, j), HullPoint(i + 1, j + 1), HullPoint(i, j + 1));
        var transomCenter = new Vector3d(-halfLength, 0, (Sheer(-halfLength) + Keel(-halfLength)) / 2);
        for (var j = 0; j < around; j++)
            BoatPresentation.Triangle(hull, SceneFrames.ToGodot(transomCenter), SceneFrames.ToGodot(HullPoint(0, j + 1)),
                SceneFrames.ToGodot(HullPoint(0, j)));
        hull.SurfaceEnd();
        BoatPresentation.AddMesh(this, hull, Vector3.Zero, new(0.9f, 0.91f, 0.88f));

        // Deck at the sheer, open over the cockpit.
        var deck = new ImmediateMesh();
        deck.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < stations; i++)
        {
            var x0 = -halfLength + length * i / stations;
            var x1 = -halfLength + length * (i + 1) / stations;
            var open = (x0 + x1) / 2 is > CockpitAftX and < CockpitForwardX;
            foreach (var side in new[] { -1, 1 })
            {
                double Inner(double x) => open ? Math.Min(CockpitHalfWidth, HalfBeam(x, geometry.HullBeamM)) : 0;
                Quad(deck, new(x0, side * Inner(x0), Sheer(x0)), new(x1, side * Inner(x1), Sheer(x1)),
                    new(x1, side * HalfBeam(x1, geometry.HullBeamM), Sheer(x1)), new(x0, side * HalfBeam(x0, geometry.HullBeamM), Sheer(x0)));
            }
        }
        deck.SurfaceEnd();
        BoatPresentation.AddMesh(this, deck, Vector3.Zero, new(0.82f, 0.8f, 0.72f));

        var cockpitLength = CockpitForwardX - CockpitAftX;
        var cockpitX = (CockpitForwardX + CockpitAftX) / 2;
        Box(new(cockpitLength, 2 * CockpitHalfWidth, 0.03), new(cockpitX, 0, CockpitSoleZ), new(0.55f, 0.56f, 0.53f));
        foreach (var side in new[] { -1, 1 })
        {
            Box(new(cockpitLength, 0.03, SeatTopZ - SheerZ + 0.3), new(cockpitX, side * CockpitHalfWidth, (SheerZ + CockpitSoleZ) / 2),
                new(0.75f, 0.75f, 0.7f));
            Box(new(cockpitLength - 0.3, 0.32, CockpitSoleZ - SeatTopZ), new(cockpitX + 0.15, side * (CockpitHalfWidth - 0.16),
                (SeatTopZ + CockpitSoleZ) / 2), new(0.7f, 0.62f, 0.48f));
        }
        Box(new(0.03, 2 * CockpitHalfWidth, CockpitSoleZ - SheerZ), new(CockpitForwardX, 0, (SheerZ + CockpitSoleZ) / 2), new(0.75f, 0.75f, 0.7f));
        // A low cabin top: the estimated main tack is only 0.65 m above the waterline.
        var cabinLength = CabinForwardX - CockpitForwardX;
        Box(new(cabinLength, 1.5, SheerZ - CabinTopZ), new((CabinForwardX + CockpitForwardX) / 2, 0, (SheerZ + CabinTopZ) / 2),
            new(0.86f, 0.86f, 0.8f));
        foreach (var side in new[] { -1, 1 })
            Box(new(1.1, 0.01, 0.07), new(0.2, side * 0.755, (SheerZ + CabinTopZ) / 2), new(0.12f, 0.16f, 0.2f));

        var mastFoot = new Vector3d(0.5, 0, CabinTopZ);
        Spar(mastFoot, new(0.5, 0, -0.65 - 7.8232 - 0.25), 0.055f, new(0.72f, 0.74f, 0.75f));
        Spar(new(3.0908, 0, -0.45), new(0.5, 0, -8.2224), 0.008f, new(0.35f, 0.36f, 0.37f));
        Boom = Spar(new(0.5, 0, -0.65), new(-2.548, 0, -0.65), 0.045f, new(0.72f, 0.74f, 0.75f));

        Board = BoatPresentation.AddMesh(this, new BoxMesh { Size = new(0.04f, 1, 0.55f) }, Vector3.Zero, new(0.2f, 0.22f, 0.24f));
        Rudder = new Node3D { Name = "Rudder", Position = SceneFrames.ToGodot(geometry.RudderTopBodyM) };
        AddChild(Rudder);
        var span = (float)(geometry.RudderTipBodyM - geometry.RudderTopBodyM).Length;
        BoatPresentation.AddMesh(Rudder, new BoxMesh { Size = new(0.04f, span, 0.4f) }, new(0, -span / 2, 0.1f), new(0.92f, 0.92f, 0.9f));
        var stockTop = (float)(geometry.RudderTopBodyM.Z - SheerZ);
        BoatPresentation.AddMesh(Rudder, new BoxMesh { Size = new(0.05f, stockTop, 0.06f) }, new(0, stockTop / 2, 0), new(0.3f, 0.3f, 0.3f));
        BoatPresentation.AddMesh(Rudder, new BoxMesh { Size = new(0.04f, 0.04f, 1.2f) }, new(0, stockTop + 0.08f, -0.6f), new(0.55f, 0.38f, 0.2f));

        Sailor = new Node3D { Name = "Skipper" };
        AddChild(Sailor);
        // Seated figure around the published skipper mass point.
        BoatPresentation.AddMesh(Sailor, new CapsuleMesh { Radius = 0.17f, Height = 0.6f }, new(0, 0.12f, 0), new(0.92f, 0.3f, 0.1f));
        BoatPresentation.AddMesh(Sailor, new SphereMesh { Radius = 0.11f, Height = 0.22f }, new(0, 0.53f, 0), new(0.9f, 0.73f, 0.54f));
        BoatPresentation.AddMesh(Sailor, new BoxMesh { Size = new(0.3f, 0.14f, 0.5f) }, new(0, -0.18f, -0.22f), new(0.12f, 0.17f, 0.2f));

        var mainCloth = BoatPresentation.AddMesh(this, MainMesh, Vector3.Zero, new(0.98f, 0.95f, 0.84f));
        var jibCloth = BoatPresentation.AddMesh(this, JibMesh, Vector3.Zero, new(0.93f, 0.95f, 0.9f));
        BoatPresentation.AddMesh(this, seams, Vector3.Zero, new(0.45f, 0.44f, 0.37f), unshaded: true);
        foreach (var node in new[] { BoatPresentation.AddMesh(this, ribbons, Vector3.Zero, Colors.White, unshaded: true),
                     BoatPresentation.AddMesh(this, sheets, Vector3.Zero, Colors.White, unshaded: true) })
            ((StandardMaterial3D)node.MaterialOverride).VertexColorUseAsAlbedo = true;
    }

    public void Render(SimulationState state)
    {
        var geometry = state.HunterGeometry!;
        var rig = state.HunterRig!;
        var datum = state.Boat.PositionWorldM - state.Boat.OrientationBodyToWorld.Rotate(state.Mass!.CenterOfMassBodyM);
        Transform = new(new Basis(SceneFrames.ToGodot(state.Boat.OrientationBodyToWorld)), SceneFrames.ToGodot(datum));
        Board.Position = SceneFrames.ToGodot((geometry.CenterboardTopBodyM + geometry.CenterboardTipBodyM) * 0.5);
        Board.Scale = new(1, Math.Max(0.01f, (float)(geometry.CenterboardTipBodyM - geometry.CenterboardTopBodyM).Length), 1);
        // Same sign convention as the foil model: positive tiller turns the trailing edge to starboard.
        Rudder.Rotation = new(0, (float)(0.45 * state.Input.Controls.Tiller), 0);
        Sailor.Position = SceneFrames.ToGodot(geometry.SkipperBodyM);
        Place(Boom, rig.Main.TackBodyM, rig.Main.ClewBodyM);
        seams.ClearSurfaces();
        ribbons.ClearSurfaces();
        seams.SurfaceBegin(Mesh.PrimitiveType.Lines);
        ribbons.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Sail(MainMesh, SailingVisuals.HunterStrips(rig.Main), state.TimeSeconds, 0.27);
        Sail(JibMesh, SailingVisuals.HunterStrips(rig.Jib), state.TimeSeconds, 0.15);
        seams.SurfaceEnd();
        ribbons.SurfaceEnd();
        Sheets(rig);
    }

    // Full beam aft to a transom of about 70 percent; a fine bow.
    private static double HalfBeam(double x, double beam)
    {
        var u = x / (Hunter235Geometry.HullLengthM / 2);
        return beam / 2 * (u >= 0 ? Math.Pow(Math.Max(0, 1 - u * u), 0.7) : 1 - 0.3 * u * u);
    }

    private static double Sheer(double x)
    {
        var u = x / (Hunter235Geometry.HullLengthM / 2);
        return SheerZ - (u < 0 ? 0.05 * u * u : 0);
    }

    // The sampled hull bottom on the centreline, faired up to the sheer at the stem.
    private static double Keel(double x)
    {
        static double Bottom(double x) => 0.48 - 0.28 * Math.Pow(x / (Hunter235HullModel.WaterlineLengthM / 2), 2);
        var halfLength = Hunter235Geometry.HullLengthM / 2;
        return x <= 0 ? Bottom(x) : Bottom(x) + (Sheer(halfLength) - Bottom(halfLength)) * Math.Pow(x / halfLength, 2.2);
    }

    private void Sail(ImmediateMesh mesh, SailStrip[] rows, double time, double telltaleChord)
    {
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (var row = 0; row < rows.Length - 1; row++)
        for (var column = 0; column < 10; column++)
        {
            Vector3 Point(int r, int c) => SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[r], c / 10.0, time));
            BoatPresentation.Triangle(mesh, Point(row, column), Point(row + 1, column), Point(row + 1, column + 1));
            BoatPresentation.Triangle(mesh, Point(row, column), Point(row + 1, column + 1), Point(row, column + 1));
        }
        mesh.SurfaceEnd();
        for (var row = 1; row < rows.Length - 1; row++)
        for (var i = 0; i < 10; i++)
        {
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[row], i / 10.0, time)));
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[row], (i + 1) / 10.0, time)));
        }
        for (var row = 0; row < rows.Length - 1; row++)
        {
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[row], 1, time)));
            seams.SurfaceAddVertex(SceneFrames.ToGodot(SailingVisuals.SailPoint(rows[row + 1], 1, time)));
        }
        // Published strips 2, 4 and 5 (rows offset by the foot row).
        foreach (var index in new[] { 2, 4, 5 })
        foreach (var side in new[] { -1, 1 })
        for (var i = 0; i < 6; i++)
        {
            ribbons.SurfaceSetColor(side < 0 ? Port : Starboard);
            var a = SceneFrames.ToGodot(SailingVisuals.TelltalePoint(rows[index], side, i / 6.0, time, telltaleChord));
            var b = SceneFrames.ToGodot(SailingVisuals.TelltalePoint(rows[index], side, (i + 1) / 6.0, time, telltaleChord));
            var transverse = (b - a).Cross(SceneFrames.ToGodot(rows[index].Normal));
            var width = (transverse.LengthSquared() > 1e-10f ? transverse.Normalized() : Vector3.Up) * 0.014f;
            BoatPresentation.Triangle(ribbons, a - width, b - width, b + width);
            BoatPresentation.Triangle(ribbons, a - width, b + width, a + width);
        }
    }

    // Jib sheets from lead to clew: bright when loaded, dim and sagging when slack.
    private void Sheets(HunterRigEvaluation rig)
    {
        sheets.ClearSurfaces();
        sheets.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var sheet in new[] { rig.PortSheet, rig.StarboardSheet })
        {
            if (sheet is not { } s) continue;
            var slack = Math.Max(0, s.SlackLengthM - s.LengthM);
            var color = slack > 0 ? new Color(0.45f, 0.45f, 0.4f) : new Color(0.95f, 0.85f, 0.25f);
            for (var i = 0; i < 8; i++)
            {
                Vector3 Point(int k)
                {
                    var t = k / 8.0;
                    return SceneFrames.ToGodot(s.LeadBodyM + (s.ClewBodyM - s.LeadBodyM) * t + Vector3d.UnitZ * (slack * 0.5 * 4 * t * (1 - t)));
                }
                sheets.SurfaceSetColor(color);
                sheets.SurfaceAddVertex(Point(i));
                sheets.SurfaceSetColor(color);
                sheets.SurfaceAddVertex(Point(i + 1));
            }
        }
        sheets.SurfaceEnd();
    }

    private static void Quad(ImmediateMesh mesh, Vector3d a, Vector3d b, Vector3d c, Vector3d d)
    {
        BoatPresentation.Triangle(mesh, SceneFrames.ToGodot(a), SceneFrames.ToGodot(b), SceneFrames.ToGodot(c));
        BoatPresentation.Triangle(mesh, SceneFrames.ToGodot(a), SceneFrames.ToGodot(c), SceneFrames.ToGodot(d));
    }

    /// <summary>A box sized and centred in body axes (X length, Y width, Z depth).</summary>
    private void Box(Vector3d size, Vector3d center, Color color) =>
        BoatPresentation.AddMesh(this, new BoxMesh { Size = new((float)size.Y, (float)size.Z, (float)size.X) },
            SceneFrames.ToGodot(center), color);

    private MeshInstance3D Spar(Vector3d from, Vector3d to, float radius, Color color)
    {
        var node = BoatPresentation.AddMesh(this, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 1 }, Vector3.Zero, color);
        Place(node, from, to);
        return node;
    }

    // Stretches a unit-height Y-axis mesh between two body points.
    private static void Place(Node3D node, Vector3d from, Vector3d to)
    {
        var a = SceneFrames.ToGodot(from);
        var b = SceneFrames.ToGodot(to);
        var axis = b - a;
        var up = axis.Normalized();
        var side = Mathf.Abs(up.Dot(Vector3.Right)) < 0.9f ? Vector3.Right : Vector3.Forward;
        var x = side.Cross(up).Normalized();
        var z = x.Cross(up);
        node.Transform = new(new Basis(x, up * axis.Length(), z), (a + b) / 2);
    }
}
