using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

/// <summary>Reproducible free-sailing baselines. These are model observations, never reference polars.</summary>
public static class CalibrationReview
{
    public sealed record Condition(string Id, double WindKnots, double HeadingDegrees, WavePreset Waves,
        int Seed, double InitialSpeedMps, SailingControls Controls);

    public sealed record Sample(double TimeSeconds, double SpeedMps, double HeadingDegrees, double HeelDegrees,
        double PitchDegrees, double BoomDegrees, double MeanLuffing, double SailDriveN);

    public sealed record Window(double StartSeconds, double EndSeconds, int Samples, double MeanSpeedMps,
        double MinSpeedMps, double MaxSpeedMps, double SpeedChangeMps, double MaxHeadingErrorDegrees,
        double MaxAbsHeelDegrees, bool SteadyCandidate);

    public sealed record Result(Condition Condition, double WindFromDegrees, string Current, double BoatMassKg,
        double SailorMassKg, double StepSeconds, int Steps, int WindowSteps, HeldSailingInput HeldThroughout,
        RigidBodyState InitialBoat, RigState InitialRig, Window FinalWindow, Sample[] Trace,
        RigidBodyState FinalBoat, RigState FinalRig, bool ExactReplay)
    {
        public WaveDirectionMode WaveDirectionMode => WaveDirectionMode.AlignedWithWind;
        public double WaveFromDegrees => 0;
    }

    public static IReadOnlyList<Condition> Conditions { get; } = Array.AsReadOnly(new[]
    {
        new Condition("coast-calm", 0, 0, WavePreset.Calm, 123, 3, new()),
        new Condition("head-to-wind-calm", 10, 0, WavePreset.Calm, 123, 0, new()),
        new Condition("upwind-45-calm", 10, 45, WavePreset.Calm, 123, 0, Trim(0.85)),
        new Condition("reach-90-calm", 10, 90, WavePreset.Calm, 123, 0, Trim(0.6)),
        new Condition("broad-reach-135-calm", 10, 135, WavePreset.Calm, 123, 0, Trim(0.3)),
        new Condition("run-180-calm", 10, 180, WavePreset.Calm, 123, 0, Trim(0.2)),
        new Condition("default-reach-gentle", 10, 90, WavePreset.Gentle, 123, 0, Trim(0.6))
    });

    private static SailingControls Trim(double sheet) => new(mainsheet: sheet, traveler: 0.8, vang: 0.6,
        outhaul: 0.5, cunningham: 0.3, hiking: -0.3);

    public static Result Run(Condition condition, int steps = 1800, int windowSteps = 600)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (steps < 2 || windowSteps < 2 || windowSteps > steps)
            throw new ArgumentOutOfRangeException(nameof(steps));
        var environment = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: condition.WindKnots,
            windFromDegrees: 0, wavePreset: condition.Waves, waveSeed: condition.Seed);
        var input = new SimulationInput(condition.Controls, environment);
        var model = new IlcaBoatModel();
        var initial = model.CreateInitialState(input, condition.HeadingDegrees * Math.PI / 180);
        initial = initial with { LinearVelocityWorldMps = initial.OrientationBodyToWorld.Rotate(new(condition.InitialSpeedMps, 0, 0)) };
        var massModel = new IlcaMassModel();
        var contributors = massModel.Contributors(condition.Controls);
        var first = new Simulation(initial, massModel, model, input);
        var second = new Simulation(initial, new IlcaMassModel(), new IlcaBoatModel(), input);
        var initialRig = first.Latest.Rig;
        var trace = new List<Sample> { Observe(first.Latest) };
        var tail = new List<Sample>(windowSteps);
        for (var step = 1; step <= steps; step++)
        {
            input = new(HeldSailingInput.Released.Advance(input.Controls), environment);
            var a = first.Step(input);
            var b = second.Step(input);
            RequireSame(a, b);
            if (step > steps - windowSteps) tail.Add(Observe(a));
            if (step % 60 == 0 || step == steps) trace.Add(Observe(a));
        }
        var final = first.Latest;
        return new(condition, 0, "Still water; wave orbital flow when non-calm",
            contributors.Where(c => c.Name != "Sailor").Sum(c => c.MassKg),
            contributors.Single(c => c.Name == "Sailor").MassKg, Simulation.StepSeconds,
            steps, windowSteps, HeldSailingInput.Released, initial, initialRig,
            Summarize(tail, condition.HeadingDegrees), trace.ToArray(), final.Boat, final.Rig, true);
    }

    /// <summary>Provisional screening tolerances, not empirical ILCA accuracy bounds.</summary>
    public static Window Summarize(IReadOnlyList<Sample> samples, double targetHeadingDegrees)
    {
        if (samples.Count < 2) throw new ArgumentException("Need two or more observations.", nameof(samples));
        var half = samples.Count / 2;
        var change = samples.Skip(half).Average(s => s.SpeedMps) - samples.Take(half).Average(s => s.SpeedMps);
        var headingError = samples.Max(s => Math.Abs(Math.IEEERemainder(s.HeadingDegrees - targetHeadingDegrees, 360)));
        var heel = samples.Max(s => Math.Abs(s.HeelDegrees));
        var minimum = samples.Min(s => s.SpeedMps);
        var maximum = samples.Max(s => s.SpeedMps);
        return new(samples[0].TimeSeconds, samples[^1].TimeSeconds, samples.Count, samples.Average(s => s.SpeedMps),
            minimum, maximum, change, headingError, heel,
            Math.Abs(change) <= 0.05 && maximum - minimum <= 0.1 && headingError <= 3 && heel < 45);
    }

    public static string ReportJson()
    {
        static object Identity(Assembly assembly) => new
        {
            Name = assembly.GetName().Name,
            Configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
        };
        var results = Conditions.Select(c => Run(c)).ToArray();
        return JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            Evidence = "Internal model consistency and free-sailing baseline; uncalibrated; no measured speed/pointing targets",
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Assemblies = new[] { Identity(typeof(Simulation).Assembly), Identity(typeof(HeldSailingInput).Assembly),
                Identity(typeof(CalibrationReview).Assembly) },
            Protocol = "Start from solved upright hydrostatic balance with initial boom 0.1 rad. Release all keys for 30 s; no heading/balance controller. Trace every 60 steps and at final step. Evaluate all final 600 steps.",
            Screening = "Steady candidate only if final 10 s: half-window speed mean change <= 0.05 m/s, speed range <= 0.1 m/s, heading error <= 3 deg, |heel| < 45 deg. Design tolerances, not measured accuracy.",
            EmpiricalTargets = Array.Empty<object>(),
            Results = results
        }, new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    private static Sample Observe(SimulationState state)
    {
        var b = state.Boat;
        var attitude = PresentationConversions.GetAttitude(b.OrientationBodyToWorld);
        return new(state.TimeSeconds, Math.Sqrt(b.LinearVelocityWorldMps.X * b.LinearVelocityWorldMps.X +
            b.LinearVelocityWorldMps.Y * b.LinearVelocityWorldMps.Y), attitude.Heading * 180 / Math.PI,
            attitude.Heel * 180 / Math.PI, attitude.Pitch * 180 / Math.PI, state.Rig.BoomAngleRad * 180 / Math.PI,
            state.Diagnostics!.SailShape.Sections.Average(s => s.Luffing),
            b.OrientationBodyToWorld.Conjugate().Rotate(state.Diagnostics.Sail.ForceWorldN).X);
    }

    private static void RequireSame(SimulationState a, SimulationState b)
    {
        var x = a.Diagnostics!;
        var y = b.Diagnostics!;
        if (a.StepIndex != b.StepIndex || a.TimeSeconds != b.TimeSeconds || a.Input != b.Input ||
            a.Boat != b.Boat || a.Mass != b.Mass || a.Rig != b.Rig || x.Gravity != y.Gravity ||
            x.Buoyancy != y.Buoyancy || x.HullDrag != y.HullDrag || x.Centerboard != y.Centerboard ||
            x.Rudder != y.Rudder || x.Sail != y.Sail || x.SailShape.BoomAngleRad != y.SailShape.BoomAngleRad ||
            x.SailShape.SheetLimitRad != y.SailShape.SheetLimitRad || !x.SailShape.Sections.SequenceEqual(y.SailShape.Sections))
            throw new InvalidOperationException($"Calibration replay differs at step {a.StepIndex}.");
    }
}
