# Sailing Simulator

Godot/C# sailing simulator in development. [PLAN.md](PLAN.md) defines the scope
and [phases/README.md](phases/README.md) tracks implementation. The current
model advances a six-degree-of-freedom ILCA 7 with sail trim, separate foils,
sampled hull forces, and hiking in steady wind over flat water. Its physics
coefficients and component masses are provisional; wave integration, the
playable Godot scene, and performance calibration follow in later phases.

Use a .NET 8 SDK. No third-party packages or Godot installation are needed to
build the foundation. `global.json` selects the latest installed .NET 8 SDK
feature band.

```sh
dotnet build SailingSimulator.sln
dotnet run --project tests/CalibrationConditionCheck
dotnet run --project tests/SimulationCoreCheck
dotnet run --project tests/BoatPhysicsCheck
dotnet run --project tools/SimulationHarness -- --steps 600
dotnet run --project tools/SimulationHarness -- --boat --steps 600
```

The harness runs two identical trajectories, compares every completed boat,
mass, rig and force/section state, and prints the final result as JSON. Default
mode uses synthetic coasting bodies; `--boat` selects the uncalibrated ILCA
model in a 10-knot beam wind. It runs offline as fast as possible; it does not
measure wall-clock cadence or establish measured ILCA performance.
Its step count defaults to 600 and accepts 1 through 1,000,000.

| Path | Purpose |
| --- | --- |
| `src/SailingSimulator.Core` | Godot-independent RK4, mass, hull/foil/sail forces and diagnostics |
| `src/SailingSimulator.Adapters` | UI units, compass settings, and explicit Godot frame conversion |
| `tools/SimulationHarness` | Deterministic offline stepping example |
| `tests` | Dependency-free executable regression checks |
| `godot` | Host integration handoff for Phase 5 |

See [the core API and numerical conventions](docs/simulation-core.md) before
implementing a force source or connecting rendering.
See [the initial boat physics](docs/boat-physics.md) for equations, controls,
mass estimates, limitations and the wave-model handoff.
