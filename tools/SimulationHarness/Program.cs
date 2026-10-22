using System.Text.Json;
using SailingSimulator.Adapters;
using SailingSimulator.Core;

var steps = 600;
if (args.Length > 0 && (args.Length != 2 || args[0] != "--steps" || !int.TryParse(args[1], out steps) || steps < 1 || steps > 1_000_000))
{
    Console.Error.WriteLine("Usage: SimulationHarness [--steps 1..1000000]");
    return 2;
}

// Synthetic unit-mass coasting body, not an ILCA model or calibration result.
var input = new SimulationInput(new SailingControls(), PresentationConversions.EnvironmentFromSettings());
var initial = RigidBodyState.AtRest with
{
    LinearVelocityWorldMps = Vector3d.UnitX,
    AngularVelocityBodyRadPerSec = new(0, 0, 0.1)
};
var mass = new MassProperties(1, new(1, 1, 1));
var first = new Simulation(initial, mass, new CompositeForceModel(), input);
var second = new Simulation(initial, mass, new CompositeForceModel(), input);
for (var i = 0; i < steps; i++)
{
    if (first.Step(input) != second.Step(input))
        throw new InvalidOperationException($"Determinism failed at step {i + 1}.");
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    Scenario = "Phase 2 synthetic coasting body; no sailing forces",
    DeterministicReplay = true,
    Snapshot = first.Latest
}, new JsonSerializerOptions { WriteIndented = true }));
return 0;
