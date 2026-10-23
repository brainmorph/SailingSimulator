using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

/// <summary>UI units and from-bearings, retained even at zero wind; edits preserve the realization seed.</summary>
public sealed record DebugEnvironmentSettings(double WindSpeedKnots = 10, double WindFromDegrees = 0,
    WavePreset WavePreset = WavePreset.Gentle, bool AlignWaves = true, double WaveFromDegrees = 0, int WaveSeed = 1)
{
    public EnvironmentConfig Build() => PresentationConversions.EnvironmentFromSettings(WindSpeedKnots, WindFromDegrees,
        WavePreset, AlignWaves ? WaveDirectionMode.AlignedWithWind : WaveDirectionMode.Independent,
        AlignWaves ? null : WaveFromDegrees, WaveSeed);

    public static DebugEnvironmentSettings FromEnvironment(EnvironmentConfig environment)
    {
        var wind = environment.TrueWindVelocityWorldMps;
        var bearing = FromBearing(wind.Length > 0 ? wind : environment.WaveTravelDirectionWorld);
        return new(PresentationConversions.MetersPerSecondToKnots(wind.Length), bearing, environment.WavePreset,
            environment.WaveDirectionMode == WaveDirectionMode.AlignedWithWind,
            FromBearing(environment.WaveTravelDirectionWorld), environment.WaveSeed);
    }

    public static double FromBearing(Vector3d travel)
    {
        var degrees = PresentationConversions.RadiansToDegrees(Math.Atan2(-travel.Y, -travel.X));
        return (degrees + 360) % 360;
    }
}
