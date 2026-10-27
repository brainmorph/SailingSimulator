using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

/// <summary>Provisional fixed-step Hunter maneuver inputs, shared by the host and offline replay.</summary>
public static class HunterDevelopmentScenario
{
    public static SailingControls InitialControls => new(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
        outhaul: 0.5, cunningham: 0.3, portJibSheet: 0.65);
    public static EnvironmentConfig Environment() => PresentationConversions.EnvironmentFromSettings(
        wavePreset: WavePreset.Calm, waveSeed: 123);
    public const long EndStep = 3720;

    // Step is the number already completed; the returned input is for the next step.
    public static int Stage(long step) => step < 240 ? 0 : step < 1200 ? 1 : step < 1380 ? 2 :
        step < 2640 ? 3 : step < 3240 ? 4 : step < 3480 ? 5 : step < EndStep ? 6 : 7;

    public static HeldSailingInput Input(long step) => Stage(step) switch
    {
        1 or 3 => new(tiller: -0.6),
        2 => new(tiller: -0.6, portJibSheet: -1, starboardJibSheet: 1),
        5 => new(centerboard: -1, hiking: 1),
        6 => new(centerboard: 1, hiking: -1),
        _ => HeldSailingInput.Released
    };
}
