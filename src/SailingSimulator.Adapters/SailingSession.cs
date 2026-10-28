using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

public enum BoatChoice { Ilca7, Hunter235 }

/// <summary>Selectable boats and the controls each new session starts from.</summary>
public static class BoatCatalog
{
    public static IReadOnlyList<BoatChoice> All { get; } = Array.AsReadOnly(new[] { BoatChoice.Ilca7, BoatChoice.Hunter235 });

    public static string Name(BoatChoice boat) => boat switch
    {
        BoatChoice.Ilca7 => "ILCA 7",
        BoatChoice.Hunter235 => "Hunter 23.5",
        _ => throw new ArgumentOutOfRangeException(nameof(boat))
    };

    public static IBoatDefinition Definition(BoatChoice boat) => boat switch
    {
        BoatChoice.Ilca7 => new IlcaBoatDefinition(),
        BoatChoice.Hunter235 => new Hunter235BoatDefinition(),
        _ => throw new ArgumentOutOfRangeException(nameof(boat))
    };

    // Both start on a beam reach in the default northerly wind, crew to windward (port).
    // The Hunter trim is the fastest 120-degree trim from the Phase 9 sweep, on the leeward jib sheet.
    public static SailingControls InitialControls(BoatChoice boat) => boat switch
    {
        BoatChoice.Ilca7 => new(mainsheet: 0.6, traveler: 0.8, vang: 0.6, outhaul: 0.5, cunningham: 0.3, hiking: -0.3),
        BoatChoice.Hunter235 => new(mainsheet: 0.7, traveler: 0.8, vang: 0.6, outhaul: 0.5, cunningham: 0.3,
            hiking: -1, starboardJibSheet: 0.7),
        _ => throw new ArgumentOutOfRangeException(nameof(boat))
    };

    /// <summary>Accepts the boat definition ids, for example "ilca-7" and "hunter-235".</summary>
    public static bool TryParse(string? id, out BoatChoice boat)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(Definition(candidate).Id, id, StringComparison.OrdinalIgnoreCase))
            {
                boat = candidate;
                return true;
            }
        }
        boat = default;
        return false;
    }
}

/// <summary>
/// One boat's simulation worker. A restart joins this worker before creating the next, so stale
/// input or snapshots from the previous boat cannot reach the new session.
/// </summary>
public sealed class SailingSession : IDisposable
{
    public const double StartHeadingRad = Math.PI / 2;
    public BoatChoice Boat { get; }
    public SimulationRunner Runner { get; }

    private SailingSession(BoatChoice boat, SimulationRunner runner)
    {
        Boat = boat;
        Runner = runner;
    }

    /// <summary>Creates a session that has not started; the caller renders its initial state, then starts it.</summary>
    public static SailingSession Create(BoatChoice boat, EnvironmentConfig environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var input = new SimulationInput(BoatCatalog.InitialControls(boat), environment);
        return new(boat, new SimulationRunner(BoatCatalog.Definition(boat).CreateSimulation(input, StartHeadingRad)));
    }

    /// <summary>
    /// Retains the supplied wind, waves and seed; resets time, motion, rig and controls. This session
    /// is stopped and joined before the replacement is created.
    /// </summary>
    public SailingSession Restart(BoatChoice boat, EnvironmentConfig environment)
    {
        Dispose();
        return Create(boat, environment);
    }

    public void Dispose()
    {
        Runner.PublishInput(new(HeldSailingInput.Released, Runner.Latest.State.Input.Environment));
        Runner.Dispose();
    }
}
