using System.Diagnostics;

namespace SailingSimulator.Core;

public enum SimulationSubsystem { Mass, Rk4, Diagnostics, Hull, Centerboard, Rudder, Sail }

/// <summary>Wall-clock observations only; never used by the physics equations.</summary>
public readonly record struct StepProfile(double MassMs, double Rk4Ms, double DiagnosticsMs,
    double HullMs, double CenterboardMs, double RudderMs, double SailMs);

/// <summary>Optional, single-owner collector. Hull/foil/sail totals nest inside RK4 and diagnostics.</summary>
public sealed class StepProfiler
{
    private readonly long[] ticks = new long[7];
    public void Reset() => Array.Clear(ticks);
    public Scope Measure(SimulationSubsystem subsystem) => new(this, subsystem);
    public StepProfile Snapshot()
    {
        var ms = 1000.0 / Stopwatch.Frequency;
        return new(ticks[0] * ms, ticks[1] * ms, ticks[2] * ms, ticks[3] * ms,
            ticks[4] * ms, ticks[5] * ms, ticks[6] * ms);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly StepProfiler owner;
        private readonly SimulationSubsystem subsystem;
        private readonly long start;
        internal Scope(StepProfiler owner, SimulationSubsystem subsystem)
        { this.owner = owner; this.subsystem = subsystem; start = Stopwatch.GetTimestamp(); }
        public void Dispose() => owner.ticks[(int)subsystem] += Stopwatch.GetTimestamp() - start;
    }
}
