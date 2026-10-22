# Sailing Simulator

Godot/C# sailing simulator in development. [PLAN.md](PLAN.md) defines the scope
and [phases/README.md](phases/README.md) tracks implementation. The current
model advances a six-degree-of-freedom ILCA 7 with sail trim, separate foils,
sampled hull forces, and hiking in steady wind over seeded irregular waves.
Calm, gentle (default), and moderate water presets are available. Its physics
coefficients, component masses and wave presets are provisional. A Godot
scene connects keyboard controls to the simulation on a dedicated 60 Hz
thread. Sectional sail shape, luffing, airflow telltales, sailor/foil positions,
waves and a fading wake provide visual feedback, with switchable onboard and
third-person cameras. Debug tools and performance calibration follow in later
phases.

Use a .NET 8 SDK. No third-party packages or Godot installation are needed to
build the foundation. `global.json` selects the latest installed .NET 8 SDK
feature band.

```sh
dotnet build SailingSimulator.sln
dotnet run --project tests/CalibrationConditionCheck
dotnet run --project tests/SimulationCoreCheck
dotnet run --project tests/BoatPhysicsCheck
dotnet run --project tests/EnvironmentCheck
dotnet run --project tests/GodotIntegrationCheck
dotnet run --project tools/SimulationHarness -- --steps 600
dotnet run --project tools/SimulationHarness -- --boat --steps 600
dotnet run --project tools/SimulationHarness -- --waves --wave-seed 123 --steps 600
```

The harness runs two identical trajectories, compares every completed boat,
mass, rig and force/section state, and prints the final result as JSON. Default
mode uses synthetic coasting bodies; `--boat` selects the uncalibrated ILCA
model in a 10-knot beam wind over flat water; `--waves` selects gentle waves.
The harness uses seed 1 unless `--wave-seed` overrides it. Normal environment
creation chooses a seed when omitted; retain the published seed to repeat a
session or change settings without reshuffling waves.
The harness runs offline as fast as possible; it does not
measure wall-clock cadence or establish measured ILCA performance.
Its step count defaults to 600 and accepts 1 through 1,000,000.

| Path | Purpose |
| --- | --- |
| `src/SailingSimulator.Core` | Godot-independent RK4, mass, hull/foil/sail forces and diagnostics |
| `src/SailingSimulator.Adapters` | Units/frame conversion, held-key ramps and threaded 60 Hz runner |
| `tools/SimulationHarness` | Deterministic offline stepping example |
| `tests` | Dependency-free executable regression checks |
| `godot` | Playable Godot 4.7 .NET test scene; [launch and controls](godot/README.md) |

See [the core API and numerical conventions](docs/simulation-core.md) before
implementing a force source or connecting rendering.
See [the initial boat physics](docs/boat-physics.md) for equations, controls,
mass estimates and limitations, and [wind and waves](docs/wind-and-waves.md)
for physical presets, seeded sampling and the Godot handoff.
See [Godot integration](docs/godot-integration.md) for thread ownership,
input publication, rendering transforms, deadlines and lifecycle behavior.
See [visual feedback](docs/visual-feedback.md) for the shared sail/water
representation, camera behavior and repeatable visual review checklist.
