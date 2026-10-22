using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

/// <summary>Immutable held-key directions, sampled by the simulation owner, never by a render delta.</summary>
public sealed record HeldSailingInput
{
    public double Tiller { get; }
    public double Mainsheet { get; }
    public double Traveler { get; }
    public double Vang { get; }
    public double Outhaul { get; }
    public double Cunningham { get; }
    public double Centerboard { get; }
    public double Hiking { get; }
    public static HeldSailingInput Released { get; } = new();

    public HeldSailingInput(double tiller = 0, double mainsheet = 0, double traveler = 0,
        double vang = 0, double outhaul = 0, double cunningham = 0, double centerboard = 0, double hiking = 0)
    {
        Tiller = Direction(tiller);
        Mainsheet = Direction(mainsheet);
        Traveler = Direction(traveler);
        Vang = Direction(vang);
        Outhaul = Direction(outhaul);
        Cunningham = Direction(cunningham);
        Centerboard = Direction(centerboard);
        Hiking = Direction(hiking);
    }

    private static double Direction(double value) => double.IsFinite(value) && value is >= -1 and <= 1
        ? value : throw new ArgumentOutOfRangeException(nameof(value));

    /// <summary>Tiller returns to centre on release; trim and body position retain their settings.</summary>
    public SailingControls Advance(SailingControls before, double seconds = Simulation.StepSeconds)
    {
        ArgumentNullException.ThrowIfNull(before);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var tiller = Tiller == 0 ? Math.CopySign(Math.Max(0, Math.Abs(before.Tiller) - 1.5 * seconds), before.Tiller)
            : Move(before.Tiller, Tiller, 1.5, seconds, -1);
        var after = new SailingControls(tiller, Move(before.Mainsheet, Mainsheet, 0.25, seconds),
            Move(before.Traveler, Traveler, 0.25, seconds), Move(before.Vang, Vang, 0.25, seconds),
            Move(before.Outhaul, Outhaul, 0.25, seconds), Move(before.Cunningham, Cunningham, 0.25, seconds),
            Move(before.Centerboard, Centerboard, 0.25, seconds), Move(before.Hiking, Hiking, 0.5, seconds, -1));
        return after == before ? before : after;
    }

    private static double Move(double value, double direction, double rate, double seconds, double min = 0) =>
        Math.Clamp(value + direction * rate * seconds, min, 1);
}

/// <summary>One atomic host handoff includes held keys and the environment realization.</summary>
public sealed record HostInput(HeldSailingInput Held, EnvironmentConfig Environment);
