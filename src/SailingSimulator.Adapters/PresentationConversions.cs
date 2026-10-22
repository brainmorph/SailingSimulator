using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

// Explicit frame labels at the boundary; the Godot project will copy these into Godot's own types.
public readonly record struct GodotVector3d(double X, double Y, double Z);
public readonly record struct GodotQuaterniond(double X, double Y, double Z, double W);
public readonly record struct AttitudeRadians(double Heel, double Pitch, double Heading);

public static class PresentationConversions
{
    public const double MetersPerSecondPerKnot = 1852.0 / 3600;
    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    public static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
    public static double KnotsToMetersPerSecond(double knots) => knots * MetersPerSecondPerKnot;
    public static double MetersPerSecondToKnots(double metersPerSecond) => metersPerSecond / MetersPerSecondPerKnot;

    /// <summary>NED north/east/down maps to Godot -Z/+X/-Y. The same proper rotation maps body axes.</summary>
    public static GodotVector3d WorldToGodot(Vector3d world) => new(world.Y, -world.Z, -world.X);
    public static Vector3d GodotToWorld(GodotVector3d godot) => new(-godot.Z, godot.X, -godot.Y);

    /// <summary>Change both local and world bases: qGodot = C qNed C^-1. Mesh forward is -Z.</summary>
    public static GodotQuaterniond BodyToWorldToGodot(Quaterniond bodyToWorld)
    {
        var q = bodyToWorld.Normalized();
        return new(q.Y, -q.Z, -q.X, q.W);
    }

    public static Quaterniond GodotToBodyToWorld(GodotQuaterniond godot) =>
        new Quaterniond(-godot.Z, godot.X, -godot.Y, godot.W).Normalized();

    /// <summary>ZYX Euler display: positive heel = starboard down; pitch = bow up; heading = clockwise from north.</summary>
    public static AttitudeRadians GetAttitude(Quaterniond bodyToWorld)
    {
        var q = bodyToWorld.Normalized();
        var heel = Math.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
        var pitch = Math.Asin(Math.Clamp(2 * (q.W * q.Y - q.Z * q.X), -1, 1));
        var heading = Math.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
        if (heading < 0) heading += 2 * Math.PI;
        return new(heel, pitch, heading);
    }

    /// <summary>UI boundary only. Compass angles describe where wind/waves come FROM.</summary>
    public static EnvironmentConfig EnvironmentFromSettings(double windSpeedKnots = 10, double windFromDegrees = 0,
        WavePreset wavePreset = WavePreset.Gentle, WaveDirectionMode waveDirectionMode = WaveDirectionMode.AlignedWithWind,
        double? waveFromDegrees = null, int? waveSeed = null)
    {
        if (!double.IsFinite(windSpeedKnots) || windSpeedKnots < 0 || windSpeedKnots > 30)
            throw new ArgumentOutOfRangeException(nameof(windSpeedKnots));
        var windTravel = TravelDirection(windFromDegrees);
        Vector3d waveTravel;
        switch (waveDirectionMode)
        {
            case WaveDirectionMode.AlignedWithWind:
                if (waveFromDegrees.HasValue) throw new ArgumentException("Aligned waves do not take an independent direction.", nameof(waveFromDegrees));
                waveTravel = windTravel;
                break;
            case WaveDirectionMode.Independent:
                if (!waveFromDegrees.HasValue) throw new ArgumentException("Independent waves require a from-direction.", nameof(waveFromDegrees));
                waveTravel = TravelDirection(waveFromDegrees.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(waveDirectionMode));
        }
        return new(windTravel * KnotsToMetersPerSecond(windSpeedKnots), wavePreset, waveDirectionMode, waveTravel,
            waveSeed ?? Random.Shared.Next());
    }

    private static Vector3d TravelDirection(double fromDegrees)
    {
        if (!double.IsFinite(fromDegrees) || fromDegrees < 0 || fromDegrees > 360)
            throw new ArgumentOutOfRangeException(nameof(fromDegrees));
        var radians = DegreesToRadians(fromDegrees == 360 ? 0 : fromDegrees);
        return new(-Math.Cos(radians), -Math.Sin(radians), 0);
    }
}
