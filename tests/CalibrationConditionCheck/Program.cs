using System.Text.Json;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var fixturePath = Path.Combine(repositoryRoot, "phases", "calibration-condition.example.json");
using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
var root = document.RootElement;

Require(root.GetProperty("id").GetString() == "steady-10kt-gentle-seed-1", "Condition ID changed unexpectedly.");

var wind = root.GetProperty("wind");
var windSpeed = wind.GetProperty("speed_mps").GetDouble();
Require(Math.Abs(windSpeed - 10 * 0.514444) < 0.000005, "Wind speed must be the 10-knot default converted to m/s.");
Require(wind.GetProperty("from_degrees").GetDouble() == 0, "North wind must use a from-direction of 0 degrees.");

var waves = root.GetProperty("waves");
Require(waves.GetProperty("preset").GetString() == "Gentle", "Gentle must remain the default wave preset.");
Require(waves.GetProperty("direction_mode").GetString() == "AlignedWithWind", "Default waves must align with wind.");
Require(waves.GetProperty("from_degrees").ValueKind == JsonValueKind.Null, "Aligned waves must not specify an independent direction.");
Require(waves.GetProperty("seed").GetInt32() == 1, "Wave seed must be an integer.");

var controls = root.GetProperty("initial_controls");
RequireRange(controls, "tiller", -1, 1);
RequireRange(controls, "mainsheet", 0, 1);
RequireRange(controls, "traveler", 0, 1);
RequireRange(controls, "vang", 0, 1);
RequireRange(controls, "outhaul", 0, 1);
RequireRange(controls, "cunningham", 0, 1);
RequireRange(controls, "centerboard", 0, 1);
RequireRange(controls, "hiking", -1, 1);

var assumptions = root.GetProperty("assumptions");
Require(assumptions.GetProperty("sailor_mass_kg").GetDouble() == 80, "Reference sailor mass must remain 80 kg.");
Require(assumptions.GetProperty("water_current").GetString() == "still", "Initial conditions must use still water.");
Require(root.GetProperty("results").ValueKind == JsonValueKind.Null, "Example results must remain unset until a run is recorded.");

Console.WriteLine("Phase 1 calibration condition contract passed.");

// Screening math must reject an unsettled speed even when the half-window means happen to match.
CalibrationReview.Sample Sample(double t, double speed, double heading = 0, double heel = 0) =>
    new(t, speed, heading, heel, 0, 0, 0, 0);
var stable = CalibrationReview.Summarize(new[] { Sample(1, 2, 359), Sample(2, 2, 1) }, 0);
Require(stable.SteadyCandidate && stable.MaxHeadingErrorDegrees == 1 && stable.Samples == 2,
    "Circular heading comparison or stable-screen calculation failed.");
var oscillating = CalibrationReview.Summarize(new[] { Sample(1, 1), Sample(2, 3), Sample(3, 1), Sample(4, 3) }, 0);
Require(!oscillating.SteadyCandidate && oscillating.SpeedChangeMps == 0 && oscillating.MinSpeedMps == 1 && oscillating.MaxSpeedMps == 3,
    "A matching mean must not conceal oscillating speed.");
Require(!CalibrationReview.Summarize(new[] { Sample(1, 2), Sample(2, 2, heel: 46) }, 0).SteadyCandidate,
    "Overturned motion must not qualify for steady comparison.");
var coasting = CalibrationReview.Run(CalibrationReview.Conditions.Single(c => c.Id == "coast-calm"), 120, 60);
Require(coasting.ExactReplay && coasting.Trace.Length == 3 && coasting.FinalWindow.Samples == 60 &&
    coasting.FinalWindow.StartSeconds == 61 * SailingSimulator.Core.Simulation.StepSeconds &&
    coasting.FinalWindow.EndSeconds == 2 && coasting.Trace[^1].SpeedMps < coasting.Trace[0].SpeedMps,
    "Coasting replay, time boundaries, sample count or dissipation failed.");
var gentleCondition = CalibrationReview.Conditions.Single(c => c.Id == "default-reach-gentle");
var gentle = CalibrationReview.Run(gentleCondition, 61, 30);
var repeated = CalibrationReview.Run(gentleCondition, 61, 30);
Require(gentle.FinalBoat == repeated.FinalBoat && gentle.FinalRig == repeated.FinalRig &&
    gentle.Trace.SequenceEqual(repeated.Trace) && gentle.Trace.Length == 3 && gentle.Condition.Seed == 123,
    "Seeded baseline must reproduce the same trace including a partial final second.");
Console.WriteLine("Phase 8 calibration screening, coasting and seeded replay checks passed.");

var controlled = CourseReview.Run(gentleCondition, 120, 60);
var controlledAgain = CourseReview.Run(gentleCondition, 120, 60);
Require(controlled.ExactReplay && controlled.ControlHistory.SequenceEqual(controlledAgain.ControlHistory) &&
    controlled.Trace.SequenceEqual(controlledAgain.Trace) && controlled.ControlHistory.Length == 120,
    "Controlled review must reproduce its complete input history and trajectory.");
var previous = gentleCondition.Controls;
foreach (var entry in controlled.ControlHistory)
{
    Require(entry.Held.Advance(previous) == entry.Applied, "Recorded controls must follow held-key rates.");
    previous = entry.Applied;
}
var observations = new[] {
    new CourseReview.Observation(1, 2, 0, 0, 0, 1, 1), new CourseReview.Observation(2, 2, 0, 0, 0, 1, 1),
    new CourseReview.Observation(3, 2, 0, 0, 0, 1, 1), new CourseReview.Observation(4, 2, 0, 0, 0, 1, 1) };
Require(CourseReview.Summarize(observations).Converged &&
    !CourseReview.Summarize(observations.Select((s, i) => i >= 2 ? s with { NetForceN = 30 } : s).ToArray()).Converged,
    "Convergence screen must detect changing force as well as steady motion.");
Console.WriteLine("Phase 8 controlled review replay, input rates and convergence checks passed.");
var course = CourseReview.Run(CalibrationReview.Conditions.Single(c => c.Id == "upwind-45-calm"), 7200, 1800);
Require(course.ExactReplay && course.FinalWindow.Converged && course.FinalWindow.MaxHeadingError < 3,
    "Test sailor must hold the upwind course after settling, including steady steering bias.");
Console.WriteLine("Phase 8 steady course-control regression passed.");

static void RequireRange(JsonElement controls, string field, double minimum, double maximum)
{
    var value = controls.GetProperty(field).GetDouble();
    Require(value >= minimum && value <= maximum, $"Control '{field}' must be between {minimum} and {maximum}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string FindRepositoryRoot(string startingDirectory)
{
    var directory = new DirectoryInfo(startingDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "PLAN.md")) && Directory.Exists(Path.Combine(directory.FullName, "phases")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing PLAN.md and phases/.");
}
