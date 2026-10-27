namespace SailingSimulator.Core;

/// <summary>Estimated hull-datum geometry for the Hunter development scene, in metres.</summary>
public sealed record HunterBoatGeometry(double HullLengthM, double HullBeamM, double HullHeightM,
    Vector3d SternBodyM, Vector3d CenterboardTopBodyM, Vector3d CenterboardTipBodyM,
    Vector3d RudderTopBodyM, Vector3d RudderTipBodyM, Vector3d SkipperBodyM);

public static class Hunter235Geometry
{
    public const double HullLengthM = 7.2136;
    public const double HullBeamM = 2.54;
    public const double HullHeightM = 0.95;
    public const double BoardSpanM = 1.25;
    public const double RudderSpanM = 0.8;
    public static readonly Vector3d BoardTop = new(0.15, 0, 0.15);
    public static readonly Vector3d RudderTop = new(-3.35, 0, 0.10);

    public static Vector3d SkipperPosition(SailingControls controls) => new(-1.8, 0.55 * controls.Hiking, -0.65);

    public static HunterBoatGeometry ForControls(SailingControls controls) => new(
        HullLengthM, HullBeamM, HullHeightM, new(-HullLengthM / 2, 0, 0),
        BoardTop, BoardTop + Vector3d.UnitZ * (BoardSpanM * controls.Centerboard),
        RudderTop, RudderTop + Vector3d.UnitZ * RudderSpanM, SkipperPosition(controls));
}
