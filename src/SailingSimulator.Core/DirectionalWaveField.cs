namespace SailingSimulator.Core;

/// <summary>Height is positive up from mean water; normal points out of water in NED axes.</summary>
public readonly record struct WaterSurfaceSample(double ElevationM, Vector3d NormalWorld)
{
    public double PositionWorldZM => -ElevationM;
}

public readonly record struct WaveComponent(double AmplitudeM, double WavelengthM,
    Vector3d TravelDirectionWorld, double PhaseRad)
{
    public double WavenumberRadPerM => 2 * Math.PI / WavelengthM;
    public double AngularFrequencyRadPerSec => Math.Sqrt(9.80665 * WavenumberRadPerM);
}

/// <summary>Immutable, deep-water linear directional spectrum; sampling consumes no randomness.</summary>
public sealed class DirectionalWaveField : IEquatable<DirectionalWaveField>
{
    public WavePreset Preset { get; }
    public Vector3d TravelDirectionWorld { get; }
    public int Seed { get; }
    public double SignificantHeightM => Preset switch
    {
        WavePreset.Calm => 0,
        WavePreset.Gentle => 0.12,
        WavePreset.Moderate => 0.30,
        _ => throw new InvalidOperationException()
    };
    public IReadOnlyList<WaveComponent> Components { get; }
    private readonly Mode[] modes;

    internal DirectionalWaveField(WavePreset preset, Vector3d travelDirectionWorld, int seed)
    {
        Preset = preset;
        TravelDirectionWorld = travelDirectionWorld;
        Seed = seed;
        if (preset == WavePreset.Calm)
        {
            modes = [];
            Components = Array.AsReadOnly(Array.Empty<WaveComponent>());
            return;
        }

        const int frequencyBins = 8, directionBins = 3;
        var components = new WaveComponent[frequencyBins * directionBins];
        var random = unchecked((uint)seed);
        double Next()
        {
            // Explicit integer generator, independent of System.Random/runtime implementation.
            random = unchecked(1664525u * random + 1013904223u);
            return (random + 0.5) / 4294967296.0;
        }
        var amplitudeSquared = 0.0;
        for (var i = 0; i < frequencyBins; i++)
        {
            // Stratified log-wavelength bins, 3–12 m, peaked at 6 m.
            var wavelength = 3 * Math.Pow(4, (i + 0.2 + 0.6 * Next()) / frequencyBins);
            var spectralWeight = Math.Exp(-0.5 * Math.Pow(Math.Log(wavelength / 6) / 0.40, 2));
            for (var j = 0; j < directionBins; j++)
            {
                var angle = (j - 1) * 25 * Math.PI / 180;
                var direction = new Vector3d(travelDirectionWorld.X * Math.Cos(angle) - travelDirectionWorld.Y * Math.Sin(angle),
                    travelDirectionWorld.X * Math.Sin(angle) + travelDirectionWorld.Y * Math.Cos(angle), 0);
                var amplitude = Math.Sqrt(spectralWeight * (j == 1 ? 0.5 : 0.25));
                components[i * directionBins + j] = new(amplitude, wavelength, direction, 2 * Math.PI * Next());
                amplitudeSquared += amplitude * amplitude;
            }
        }
        var scale = SignificantHeightM / (4 * Math.Sqrt(amplitudeSquared / 2));
        modes = new Mode[components.Length];
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i] with { AmplitudeM = components[i].AmplitudeM * scale };
            components[i] = component;
            modes[i] = new(component.AmplitudeM, component.WavenumberRadPerM,
                component.AngularFrequencyRadPerSec, component.TravelDirectionWorld, component.PhaseRad);
        }
        Components = Array.AsReadOnly(components);
    }

    public WaterSurfaceSample SampleSurface(Vector3d positionWorldM, double timeSeconds)
    {
        Validate(positionWorldM, timeSeconds);
        var height = 0.0;
        var normal = -Vector3d.UnitZ;
        foreach (var mode in modes)
        {
            var phase = mode.Phase(positionWorldM, timeSeconds);
            height += mode.Amplitude * Math.Cos(phase);
            normal += mode.Direction * (mode.Amplitude * mode.K * Math.Sin(phase));
        }
        return new(height, normal / normal.Length);
    }

    /// <summary>Zero-mean orbital flow, attenuated below mean water; no imposed current.</summary>
    public Vector3d VelocityAt(Vector3d positionWorldM, double timeSeconds)
    {
        Validate(positionWorldM, timeSeconds);
        var velocity = Vector3d.Zero;
        foreach (var mode in modes)
        {
            var phase = mode.Phase(positionWorldM, timeSeconds);
            var speed = mode.Amplitude * mode.Omega * Math.Exp(-mode.K * Math.Max(0, positionWorldM.Z));
            velocity += mode.Direction * (speed * Math.Cos(phase)) - Vector3d.UnitZ * (speed * Math.Sin(phase));
        }
        return velocity;
    }

    private static void Validate(Vector3d position, double time)
    {
        if (!position.IsFinite || !double.IsFinite(time)) throw new ArgumentException("Water samples require finite position and time.");
    }

    // EnvironmentConfig keeps value equality even though its cached realization owns arrays.
    public bool Equals(DirectionalWaveField? other) => other is not null &&
        Preset == other.Preset && TravelDirectionWorld == other.TravelDirectionWorld && Seed == other.Seed;
    public override bool Equals(object? obj) => obj is DirectionalWaveField other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Preset, TravelDirectionWorld, Seed);

    private readonly record struct Mode(double Amplitude, double K, double Omega, Vector3d Direction, double InitialPhase)
    {
        public double Phase(Vector3d position, double time) => K * (Direction.X * position.X + Direction.Y * position.Y) - Omega * time + InitialPhase;
    }
}
