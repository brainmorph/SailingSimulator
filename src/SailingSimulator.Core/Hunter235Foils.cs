namespace SailingSimulator.Core;

/// <summary>Estimated Hunter foil geometry using the shared uncalibrated lift/drag curves.</summary>
public sealed class Hunter235Foils
{
    public FoilModel Centerboard { get; } = new(FoilKind.Centerboard, Hunter235Geometry.BoardSpanM, 0.9, Hunter235Geometry.BoardTop);
    public FoilModel Rudder { get; } = new(FoilKind.Rudder, Hunter235Geometry.RudderSpanM, 0.45, Hunter235Geometry.RudderTop);
}
