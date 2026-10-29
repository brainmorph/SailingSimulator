using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

/// <summary>A reproducible test sailor for engineering review, not a performance target.</summary>
public static class CourseReview
{
    public sealed record Entry(int Step, HeldSailingInput Held, SailingControls Applied);
    public sealed record Observation(double Time, double Speed, double HeadingError, double Heel,
        double Pitch, double NetForceN, double NetMomentNm);
    public sealed record Summary(double StartSeconds, double EndSeconds, int Samples, double SpeedRange,
        double MeanSpeedChange, double MeanHeelChange, double MeanPitchChange,
        double MaxHeadingError, double MaxAbsHeel, double MeanNetForceN, double MeanNetMomentNm,
        double MeanNetForceChangeN, double MeanNetMomentChangeNm, bool Converged);
    public sealed record Result(CalibrationReview.Condition Condition, int Steps, int WindowSteps,
        Entry[] ControlHistory, Observation[] Trace, Summary FinalWindow, Summary[] CompleteWindows, bool ExactReplay);

    // Heading and heel feedback requests normalized control targets. Keys are held until the
    // target is reached, so the same fixed-step rates and release behaviour as the host apply.
    public sealed class TestSailor
    {
        private double headingIntegral;

        public HeldSailingInput Decide(SimulationState state, double targetHeadingDegrees)
        {
            var attitude = PresentationConversions.GetAttitude(state.Boat.OrientationBodyToWorld);
            var error = Math.IEEERemainder(targetHeadingDegrees * Math.PI / 180 - attitude.Heading, 2 * Math.PI);
            // Bound the integral contribution so prolonged course errors cannot wind it up indefinitely.
            headingIntegral = Math.Clamp(headingIntegral + 0.08 * error * Simulation.StepSeconds, -0.4, 0.4);
            var tiller = Math.Clamp(1.2 * error + headingIntegral - 0.8 * state.Boat.AngularVelocityBodyRadPerSec.Z, -0.7, 0.7);
            var hiking = Math.Clamp(-0.7 * attitude.Heel - 0.3 * state.Boat.AngularVelocityBodyRadPerSec.X, -0.8, 0.8);
            static double Key(double target, double current, double step) =>
                target > current + step / 2 ? 1 : target < current - step / 2 ? -1 : 0;
            return new(tiller: Key(tiller, state.Input.Controls.Tiller, 1.5 * Simulation.StepSeconds),
                hiking: Key(hiking, state.Input.Controls.Hiking, 0.5 * Simulation.StepSeconds));
        }
    }

    public static Result Run(CalibrationReview.Condition condition, int steps = 7200, int windowSteps = 1800)
    {
        if (steps < 2 || windowSteps < 4 || windowSteps > steps) throw new ArgumentOutOfRangeException(nameof(steps));
        var environment = PresentationConversions.EnvironmentFromSettings(windSpeedKnots: condition.WindKnots,
            windFromDegrees: 0, wavePreset: condition.Waves, waveSeed: condition.Seed);
        var input = new SimulationInput(condition.Controls, environment);
        var model = new IlcaBoatModel();
        var initial = model.CreateInitialState(input, condition.HeadingDegrees * Math.PI / 180);
        initial = initial with { LinearVelocityWorldMps = initial.OrientationBodyToWorld.Rotate(new(condition.InitialSpeedMps, 0, 0)) };
        var first = new Simulation(initial, new IlcaMassModel(), model, input);
        var replay = new Simulation(initial, new IlcaMassModel(), new IlcaBoatModel(), input);
        var sailor = new TestSailor();
        var replaySailor = new TestSailor();
        var history = new List<Entry>(steps);
        var trace = new List<Observation>();
        var tail = new List<Observation>(windowSteps);
        var window = new List<Observation>(windowSteps);
        var windows = new List<Summary>();
        for (var step = 1; step <= steps; step++)
        {
            var held = sailor.Decide(first.Latest, condition.HeadingDegrees);
            if (held != replaySailor.Decide(replay.Latest, condition.HeadingDegrees))
                throw new InvalidOperationException($"Test sailor replay differs at step {step}.");
            input = new(held.Advance(input.Controls), environment);
            history.Add(new(step, held, input.Controls));
            var state = first.Step(input);
            CalibrationReview.RequireSame(state, replay.Step(input));
            var sample = Observe(state, condition.HeadingDegrees);
            window.Add(sample);
            if (window.Count == windowSteps)
            {
                windows.Add(Summarize(window));
                window.Clear();
            }
            if (step > steps - windowSteps) tail.Add(sample);
            if (step % 60 == 0 || step == steps) trace.Add(sample);
        }
        return new(condition, steps, windowSteps, history.ToArray(), trace.ToArray(), Summarize(tail), windows.ToArray(), true);
    }

    public static Summary Summarize(IReadOnlyList<Observation> samples)
    {
        if (samples.Count < 4) throw new ArgumentException("Need at least four observations.", nameof(samples));
        var half = samples.Count / 2;
        double Change(Func<Observation, double> value) => samples.Skip(half).Average(value) - samples.Take(half).Average(value);
        var speed = Change(s => s.Speed);
        var heel = Change(s => s.Heel);
        var pitch = Change(s => s.Pitch);
        var force = Change(s => s.NetForceN);
        var moment = Change(s => s.NetMomentNm);
        var heading = samples.Max(s => Math.Abs(s.HeadingError));
        var maxHeel = samples.Max(s => Math.Abs(s.Heel));
        // These screens are provisional engineering thresholds, not real-boat accuracy criteria.
        var speedRange = samples.Max(s => s.Speed) - samples.Min(s => s.Speed);
        var converged = Math.Abs(speed) <= 0.05 && speedRange <= 0.1 &&
            Math.Abs(heel) <= 2 && Math.Abs(pitch) <= 2 && heading <= 3 && maxHeel < 45 &&
            Math.Abs(force) <= 10 && Math.Abs(moment) <= 10;
        return new(samples[0].Time, samples[^1].Time, samples.Count, speedRange, speed, heel, pitch, heading, maxHeel, samples.Average(s => s.NetForceN),
            samples.Average(s => s.NetMomentNm), force, moment, converged);
    }

    private static Observation Observe(SimulationState state, double target)
    {
        var a = PresentationConversions.GetAttitude(state.Boat.OrientationBodyToWorld);
        var d = state.Diagnostics!;
        var total = d.Gravity + d.Buoyancy + d.HullDrag + d.Centerboard + d.Rudder + d.Sail;
        var v = state.Boat.LinearVelocityWorldMps;
        return new(state.TimeSeconds, Math.Sqrt(v.X * v.X + v.Y * v.Y),
            Math.IEEERemainder(a.Heading * 180 / Math.PI - target, 360), a.Heel * 180 / Math.PI,
            a.Pitch * 180 / Math.PI, total.ForceWorldN.Length, total.MomentBodyNm.Length);
    }

    public static string ReportJson() => JsonSerializer.Serialize(new
    {
        Evidence = "Deterministic controlled model review; provisional thresholds, not measured performance",
        Runtime = RuntimeInformation.FrameworkDescription,
        OperatingSystem = RuntimeInformation.OSDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        Assemblies = new[] { typeof(Simulation).Assembly, typeof(HeldSailingInput).Assembly, typeof(CourseReview).Assembly }
            .Select(a => new { Name = a.GetName().Name, Configuration = a.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray(),
        Protocol = "120 s at 60 Hz; final 30 s screen; north 10 kt wind; fixed trim; ordinary held-key steering and hiking; per-step controls retained; independent exact replay",
        Results = CalibrationReview.Conditions.Where(c => c.Id is "upwind-45-calm" or "reach-90-calm" or "broad-reach-135-calm" or "default-reach-gentle").Select(c => Run(c)).ToArray()
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() } });

    public static string WaveReviewJson() => JsonSerializer.Serialize(new
    {
        Evidence = "Wave variability investigation; screens are provisional and do not establish equilibrium or real-boat accuracy",
        Runtime = RuntimeInformation.FrameworkDescription,
        OperatingSystem = RuntimeInformation.OSDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        Assemblies = new[] { typeof(Simulation).Assembly, typeof(HeldSailingInput).Assembly, typeof(CourseReview).Assembly }
            .Select(a => new { Name = a.GetName().Name, Configuration = a.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray(),
        Protocol = "120 s at 60 Hz, four non-overlapping 30 s windows; 10 kt north wind, fixed trim, held-key course and balance control; exact per-step replay",
        Results = new[] { 1, 123, 456 }.Select(seed => Run(CalibrationReview.Conditions.Single(c => c.Id == "default-reach-gentle") with { Seed = seed })).ToArray()
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() } });
}
