namespace SailingSimulator.Core;

/// <summary>Provisional Hunter hull columns and independent resistance estimates.</summary>
public sealed class Hunter235HullModel : IForceModel
{
    // Surge resistance estimates; not towing-tank measurements.
    public const double WaterlineLengthM = 6.5278;
    public const double WettedSurfaceM2 = 12.5;
    public const double FormFactor = 1.15;
    public const double KinematicViscosityM2PerSec = 1.14e-6;
    /// <summary>Wave resistance as a fraction of displacement weight at Froude number 0.4.</summary>
    public const double WaveResistanceAtFn04 = 0.017;
    /// <summary>
    /// Calm upright effective wet waterplane of the samples, including the shallow-column wetting
    /// factor, over which surge resistance is spread.
    /// </summary>
    public const double ReferenceWetWaterplaneM2 = 10.8467;
    private static readonly double WeightN = new Hunter235MassModel().Evaluate(new()).MassKg * BoatGeometry.GravityMps2;
    private readonly SampledHullModel hull;
    public IReadOnlyList<HullSample> Samples => hull.Samples;

    public Hunter235HullModel()
    {
        const int longitudinal = 16, transverse = 6;
        const double waterline = 6.5278, length = Hunter235Geometry.HullLengthM, beam = Hunter235Geometry.HullBeamM;
        const double dx = waterline / longitudinal;
        var samples = new List<HullSample>(longitudinal * transverse);
        for (var i = 0; i < longitudinal; i++)
        {
            var x = -waterline / 2 + (i + 0.5) * dx;
            var halfBeam = beam / 2 * Math.Sqrt(1 - Math.Pow(2 * x / length, 2));
            var dy = 2 * halfBeam / transverse;
            for (var j = 0; j < transverse; j++)
            {
                var y = -halfBeam + (j + 0.5) * dy;
                var bottom = 0.48 - 0.28 * Math.Pow(x / (waterline / 2), 2)
                    - 0.18 * Math.Pow(y / halfBeam, 2);
                samples.Add(new(new(x, y, bottom), Hunter235Geometry.HullHeightM, dx * dy));
            }
        }
        hull = new SampledHullModel(samples, new(0, 0.12, 0.85), new(0.6, 8, 40), SurgeCoefficient, new(0, 0, 0.3));
    }

    /// <summary>Upright quadratic surge resistance R(u), excluding the linear low-speed term.</summary>
    public static double UprightResistanceN(double speedMps) =>
        0.5 * BoatGeometry.WaterDensityKgM3 * SurgeCoefficient(speedMps) * ReferenceWetWaterplaneM2 * speedMps * speedMps;

    // ITTC-57 friction with a form factor, plus wave resistance rising as Fn^6, expressed per unit
    // wet waterplane so the sampled columns keep their heel, heave and wave-immersion distribution.
    private static double SurgeCoefficient(double speedMps)
    {
        var reynolds = Math.Max(1e5, speedMps * WaterlineLengthM / KinematicViscosityM2PerSec);
        var friction = 0.075 / Math.Pow(Math.Log10(reynolds) - 2, 2) * FormFactor * WettedSurfaceM2;
        var hullSpeed = 0.4 * Math.Sqrt(BoatGeometry.GravityMps2 * WaterlineLengthM);
        // WaveResistanceAtFn04 W (u / u0.4)^6 divided by the dynamic pressure 0.5 rho u^2.
        var wave = WaveResistanceAtFn04 * WeightN * Math.Pow(speedMps, 4) /
            (Math.Pow(hullSpeed, 6) * 0.5 * BoatGeometry.WaterDensityKgM3);
        return (friction + wave) / ReferenceWetWaterplaneM2;
    }

    public ForceMoment Evaluate(in ForceEvaluation evaluation) => hull.Evaluate(in evaluation);
    public HullForces EvaluateHull(in ForceEvaluation evaluation) => hull.EvaluateHull(in evaluation);

    /// <summary>Finds upright calm-water heave balance; pitch and roll remain free.</summary>
    public RigidBodyState CreateInitialState(SimulationInput input, double headingRad = 0)
    {
        ArgumentNullException.ThrowIfNull(input);
        var mass = new Hunter235MassModel().Evaluate(input.Controls);
        var orientation = Quaterniond.FromAxisAngle(Vector3d.UnitZ, headingRad);
        // Use calm water for a repeatable static starting point, even when waves are enabled.
        var calmEnvironment = new EnvironmentConfig(input.Environment.TrueWindVelocityWorldMps,
            WavePreset.Calm, input.Environment.WaveDirectionMode, input.Environment.WaveTravelDirectionWorld,
            input.Environment.WaveSeed);
        var calm = new SimulationInput(input.Controls, calmEnvironment);
        var low = -3.0;
        var high = 3.0;
        for (var i = 0; i < 60; i++)
        {
            var z = (low + high) / 2;
            var boat = new RigidBodyState(new(0, 0, z), orientation, default, default);
            var force = EvaluateHull(new(0, boat, mass, calm)).Buoyancy.ForceWorldN.Z + mass.MassKg * BoatGeometry.GravityMps2;
            if (force > 0) low = z; else high = z;
        }
        return new(new(0, 0, (low + high) / 2), orientation, default, default);
    }
}
