# Sailing Simulator

Godot/C# sailing simulator in development. [PLAN.md](PLAN.md) defines the scope
and [phases/README.md](phases/README.md) tracks implementation. The current
foundation advances a six-degree-of-freedom rigid body; sailing forces and the
playable Godot scene follow in later phases.

Use a .NET 8 SDK. No third-party packages or Godot installation are needed to
build the foundation. `global.json` selects the latest installed .NET 8 SDK
feature band.

```sh
dotnet build SailingSimulator.sln
dotnet run --project tests/CalibrationConditionCheck
dotnet run --project tests/SimulationCoreCheck
dotnet run --project tools/SimulationHarness -- --steps 600
```

The harness runs two identical synthetic coasting bodies, compares every
snapshot, and prints the final result as JSON. It runs offline as fast as
possible; it does not measure wall-clock cadence or predict ILCA performance.
Its step count defaults to 600 and accepts 1 through 1,000,000.

| Path | Purpose |
| --- | --- |
| `src/SailingSimulator.Core` | Godot-independent SI state, fixed-step RK4, and force interface |
| `src/SailingSimulator.Adapters` | UI units, compass settings, and explicit Godot frame conversion |
| `tools/SimulationHarness` | Deterministic offline stepping example |
| `tests` | Dependency-free executable regression checks |
| `godot` | Host integration handoff for Phase 5 |

See [the core API and numerical conventions](docs/simulation-core.md) before
implementing a force source or connecting rendering.
