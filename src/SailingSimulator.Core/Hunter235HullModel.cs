namespace SailingSimulator.Core;

/// <summary>Provisional Hunter hull columns and independent resistance estimates.</summary>
public sealed class Hunter235HullModel : IForceModel
{
    private readonly SampledHullModel hull;
    public IReadOnlyList<HullSample> Samples => hull.Samples;

    public Hunter235HullModel()
    {
        const int longitudinal = 16, transverse = 6;
        const double waterline = 6.5278, length = 7.2136, beam = 2.54;
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
                samples.Add(new(new(x, y, bottom), 0.95, dx * dy));
            }
        }
        hull = new SampledHullModel(samples, new(0.016, 0.12, 0.85), new(0.6, 8, 40));
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
