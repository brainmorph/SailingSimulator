using System.Text.Json;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

if (args is ["--calibration-review"])
{
    Console.WriteLine(CalibrationReview.ReportJson());
    return 0;
}
if (args is ["--course-review"])
{
    Console.WriteLine(CourseReview.ReportJson());
    return 0;
}
if (args is ["--wave-review"])
{
    Console.WriteLine(CourseReview.WaveReviewJson());
    return 0;
}

var steps = 600;
var boatScenario = false;
var waves = false;
var waveSeed = 1;
var seedSpecified = false;
var stepsSpecified = false;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--boat" && !boatScenario) boatScenario = true;
    else if (args[i] == "--waves" && !waves) { waves = true; boatScenario = true; }
    else if (args[i] == "--wave-seed" && !seedSpecified && ++i < args.Length && int.TryParse(args[i], out waveSeed))
        seedSpecified = true;
    else if (args[i] == "--steps" && !stepsSpecified && ++i < args.Length && int.TryParse(args[i], out steps) && steps is >= 1 and <= 1_000_000)
        stepsSpecified = true;
    else
    {
        Console.Error.WriteLine("Usage: SimulationHarness [--boat | --waves] [--wave-seed integer] [--steps 1..1000000] | --calibration-review | --course-review | --wave-review");
        return 2;
    }
}

var input = boatScenario ? new SimulationInput(new SailingControls(mainsheet: 0.6, traveler: 0.8, vang: 0.6,
    outhaul: 0.5, cunningham: 0.3, hiking: -0.3),
    PresentationConversions.EnvironmentFromSettings(windFromDegrees: 270, wavePreset: waves ? WavePreset.Gentle : WavePreset.Calm, waveSeed: waveSeed))
    : new SimulationInput(new SailingControls(), PresentationConversions.EnvironmentFromSettings(waveSeed: waveSeed));
var boatModel = new IlcaBoatModel();
var initial = boatScenario ? boatModel.CreateInitialState(input) : RigidBodyState.AtRest with
{
    LinearVelocityWorldMps = Vector3d.UnitX,
    AngularVelocityBodyRadPerSec = new(0, 0, 0.1)
};
var mass = new MassProperties(1, new(1, 1, 1));
Simulation Create() => boatScenario ? new(initial, new IlcaMassModel(), boatModel, input)
    : new(initial, mass, new CompositeForceModel(), input);
var first = Create();
var second = Create();
for (var i = 0; i < steps; i++)
{
    var a = first.Step(input);
    var b = second.Step(input);
    if (a.StepIndex != b.StepIndex || a.TimeSeconds != b.TimeSeconds || a.Input != b.Input ||
        a.Boat != b.Boat || a.Rig != b.Rig || a.Mass != b.Mass || !SameDiagnostics(a.Diagnostics, b.Diagnostics))
        throw new InvalidOperationException($"Determinism failed at step {i + 1}.");
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    Scenario = boatScenario ? $"Uncalibrated ILCA 7; 10-knot beam wind, {(waves ? "gentle seeded waves" : "flat still water")}" : "Phase 2 synthetic coasting body; no sailing forces",
    DeterministicReplay = true,
    Snapshot = first.Latest
}, new JsonSerializerOptions { WriteIndented = true }));
return 0;

static bool SameDiagnostics(BoatDiagnostics? a, BoatDiagnostics? b) => a is null ? b is null : b is not null &&
    a.Gravity == b.Gravity && a.Buoyancy == b.Buoyancy && a.HullDrag == b.HullDrag &&
    a.Centerboard == b.Centerboard && a.Rudder == b.Rudder && a.Sail == b.Sail &&
    a.SailShape.BoomAngleRad == b.SailShape.BoomAngleRad && a.SailShape.SheetLimitRad == b.SailShape.SheetLimitRad &&
    a.SailShape.Sections.SequenceEqual(b.SailShape.Sections);
