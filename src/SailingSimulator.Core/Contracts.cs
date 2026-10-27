namespace SailingSimulator.Core;

/// <summary>Immutable held-control snapshot; all values are normalized, not physical angles.</summary>
public sealed record SailingControls
{
    public double Tiller { get; }
    public double Mainsheet { get; }
    public double Traveler { get; }
    public double Vang { get; }
    public double Outhaul { get; }
    public double Cunningham { get; }
    public double Centerboard { get; }
    public double Hiking { get; }
    /// <summary>Normalized sheet tension, zero eased and one fully trimmed.</summary>
    public double PortJibSheet { get; }
    /// <summary>Normalized sheet tension, zero eased and one fully trimmed.</summary>
    public double StarboardJibSheet { get; }

    public SailingControls(double tiller = 0, double mainsheet = 0, double traveler = 0,
        double vang = 0, double outhaul = 0, double cunningham = 0, double centerboard = 1, double hiking = 0,
        double portJibSheet = 0, double starboardJibSheet = 0)
    {
        Tiller = InRange(tiller, -1, 1, nameof(tiller));
        Mainsheet = InRange(mainsheet, 0, 1, nameof(mainsheet));
        Traveler = InRange(traveler, 0, 1, nameof(traveler));
        Vang = InRange(vang, 0, 1, nameof(vang));
        Outhaul = InRange(outhaul, 0, 1, nameof(outhaul));
        Cunningham = InRange(cunningham, 0, 1, nameof(cunningham));
        Centerboard = InRange(centerboard, 0, 1, nameof(centerboard));
        Hiking = InRange(hiking, -1, 1, nameof(hiking));
        PortJibSheet = InRange(portJibSheet, 0, 1, nameof(portJibSheet));
        StarboardJibSheet = InRange(starboardJibSheet, 0, 1, nameof(starboardJibSheet));
    }

    private static double InRange(double value, double min, double max, string name) =>
        double.IsFinite(value) && value >= min && value <= max ? value : throw new ArgumentOutOfRangeException(name);
}

public enum WavePreset { Calm, Gentle, Moderate }
public enum WaveDirectionMode { AlignedWithWind, Independent }

/// <summary>Immutable SI wind settings and seeded water realization.</summary>
public sealed record EnvironmentConfig
{
    public Vector3d TrueWindVelocityWorldMps { get; }
    public WavePreset WavePreset { get; }
    public WaveDirectionMode WaveDirectionMode { get; }
    /// <summary>Horizontal unit propagation direction, including when wind speed is zero.</summary>
    public Vector3d WaveTravelDirectionWorld { get; }
    public int WaveSeed { get; }
    public DirectionalWaveField Water { get; }

    public EnvironmentConfig(Vector3d trueWindVelocityWorldMps, WavePreset wavePreset,
        WaveDirectionMode waveDirectionMode, Vector3d waveTravelDirectionWorld, int waveSeed)
    {
        if (!trueWindVelocityWorldMps.IsFinite || !double.IsFinite(trueWindVelocityWorldMps.Length) || trueWindVelocityWorldMps.Z != 0)
            throw new ArgumentException("Initial true wind must be finite and horizontal.", nameof(trueWindVelocityWorldMps));
        if (!Enum.IsDefined(wavePreset) || !Enum.IsDefined(waveDirectionMode))
            throw new ArgumentException("Unknown wave configuration.");
        if (!waveTravelDirectionWorld.IsFinite || waveTravelDirectionWorld.Z != 0 ||
            Math.Abs(waveTravelDirectionWorld.LengthSquared - 1) > 1e-10)
            throw new ArgumentException("Wave direction must be a horizontal unit vector.", nameof(waveTravelDirectionWorld));
        if (waveDirectionMode == WaveDirectionMode.AlignedWithWind && trueWindVelocityWorldMps.Length > 0 &&
            (trueWindVelocityWorldMps / trueWindVelocityWorldMps.Length - waveTravelDirectionWorld).Length > 1e-10)
            throw new ArgumentException("Aligned wave direction must follow the wind velocity.", nameof(waveTravelDirectionWorld));
        TrueWindVelocityWorldMps = trueWindVelocityWorldMps;
        WavePreset = wavePreset;
        WaveDirectionMode = waveDirectionMode;
        WaveTravelDirectionWorld = waveTravelDirectionWorld;
        WaveSeed = waveSeed;
        Water = new(wavePreset, waveTravelDirectionWorld, waveSeed);
    }
}

public sealed record SimulationInput
{
    public SailingControls Controls { get; }
    public EnvironmentConfig Environment { get; }
    public SimulationInput(SailingControls controls, EnvironmentConfig environment)
    {
        Controls = controls ?? throw new ArgumentNullException(nameof(controls));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }
}

/// <summary>Immutable reference exchange; callers sample once at the start of a step.</summary>
public sealed class SnapshotMailbox<T> where T : class
{
    private T latest;
    public SnapshotMailbox(T initial) => latest = initial ?? throw new ArgumentNullException(nameof(initial));
    public T Read() => Volatile.Read(ref latest);
    public void Publish(T snapshot) => Interlocked.Exchange(ref latest, snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
}

/// <summary>Position of the centre of mass; world = north/east/down (NED). Angular velocity is body-relative.</summary>
public readonly record struct RigidBodyState(
    Vector3d PositionWorldM, Quaterniond OrientationBodyToWorld,
    Vector3d LinearVelocityWorldMps, Vector3d AngularVelocityBodyRadPerSec)
{
    public static RigidBodyState AtRest => new(Vector3d.Zero, Quaterniond.Identity, Vector3d.Zero, Vector3d.Zero);

    internal RigidBodyState ValidatedAndNormalized()
    {
        if (!PositionWorldM.IsFinite || !LinearVelocityWorldMps.IsFinite || !AngularVelocityBodyRadPerSec.IsFinite)
            throw new ArgumentException("Rigid-body position and velocities must be finite.");
        return this with { OrientationBodyToWorld = OrientationBodyToWorld.Normalized() };
    }
}

public sealed record SimulationState(long StepIndex, double TimeSeconds, RigidBodyState Boat, SimulationInput Input)
{
    public MassProperties? Mass { get; init; }
    public BoatDiagnostics? Diagnostics { get; init; }
    public RigState Rig { get; init; }
}

/// <summary>Reduced-order sail angles and rates, positive towards starboard. Jib angle is about its forestay, not a boom.</summary>
public readonly record struct RigState(double BoomAngleRad, double BoomRateRadPerSec,
    double JibAngleRad = 0, double JibRateRadPerSec = 0);

/// <summary>Mass, hull-datum centre of mass, and full body-frame inertia about that centre.</summary>
public sealed record MassProperties
{
    public double MassKg { get; }
    public Vector3d CenterOfMassBodyM { get; private init; }
    public InertiaTensor InertiaBodyKgM2 { get; private init; }
    /// <summary>Diagonal entries; principal moments only when products of inertia are zero.</summary>
    public Vector3d PrincipalInertiaKgM2 => new(InertiaBodyKgM2.XX, InertiaBodyKgM2.YY, InertiaBodyKgM2.ZZ);
    public MassProperties(double massKg, Vector3d principalInertiaKgM2)
    {
        if (!double.IsFinite(massKg) || massKg <= 0) throw new ArgumentOutOfRangeException(nameof(massKg));
        if (!principalInertiaKgM2.IsFinite || principalInertiaKgM2.X <= 0 || principalInertiaKgM2.Y <= 0 || principalInertiaKgM2.Z <= 0)
            throw new ArgumentOutOfRangeException(nameof(principalInertiaKgM2));
        MassKg = massKg;
        InertiaBodyKgM2 = new(principalInertiaKgM2.X, principalInertiaKgM2.Y, principalInertiaKgM2.Z, 0, 0, 0);
    }

    public static MassProperties FromTensor(double massKg, Vector3d centerOfMassBodyM, InertiaTensor inertia)
    {
        if (!centerOfMassBodyM.IsFinite) throw new ArgumentException("Centre of mass must be finite.");
        inertia.Validate();
        return new MassProperties(massKg, new(inertia.XX, inertia.YY, inertia.ZZ))
        { CenterOfMassBodyM = centerOfMassBodyM, InertiaBodyKgM2 = inertia };
    }
}
