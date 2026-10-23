using SailingSimulator.Core;

namespace SailingSimulator.Adapters;

public sealed record PerformanceSnapshot(int Samples, double P50Ms, double P95Ms, double P99Ms, double MaxMs,
    long AllocatedBytes, double AllocationBytesPerSecond, int Gen0Collections, int Gen1Collections,
    int Gen2Collections, long ManagedHeapBytes, StepProfile Subsystems);

/// <summary>Single-writer, bounded 300-step window; nearest-rank percentiles, including warm-up.</summary>
public sealed class StepPerformanceWindow
{
    public const int Capacity = 300;
    private readonly double[] durations = new double[Capacity];
    private readonly double[] intervals = new double[Capacity];
    private readonly long[] allocations = new long[Capacity];
    private readonly double[] sorted = new double[Capacity];
    private int next, count;
    private double elapsed;
    private long allocated;

    public PerformanceSnapshot Add(double milliseconds, long bytes, double intervalSeconds,
        int gen0 = 0, int gen1 = 0, int gen2 = 0, long heapBytes = 0, StepProfile subsystems = default)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0 || bytes < 0 ||
            !double.IsFinite(intervalSeconds) || intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        elapsed += intervalSeconds - intervals[next];
        allocated += bytes - allocations[next];
        durations[next] = milliseconds;
        intervals[next] = intervalSeconds;
        allocations[next] = bytes;
        next = (next + 1) % Capacity;
        count = Math.Min(count + 1, Capacity);
        Array.Copy(durations, sorted, count);
        Array.Sort(sorted, 0, count);
        double Rank(double percentile) => sorted[Math.Max(0, (int)Math.Ceiling(percentile * count) - 1)];
        return new(count, Rank(0.50), Rank(0.95), Rank(0.99), sorted[count - 1], bytes,
            allocated / elapsed, gen0, gen1, gen2, heapBytes, subsystems);
    }
}
