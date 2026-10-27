namespace SailingSimulator.Core;

/// <summary>Estimated Hunter foil geometry using the shared uncalibrated lift/drag curves.</summary>
public sealed class Hunter235Foils
{
    public FoilModel Centerboard { get; } = new(FoilKind.Centerboard, 1.25, 0.9, new(0.15, 0, 0.15));
    public FoilModel Rudder { get; } = new(FoilKind.Rudder, 0.8, 0.45, new(-3.35, 0, 0.10));
}
