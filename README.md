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
third-person cameras. F1 provides environment controls, live telemetry/graphs,
deadline warnings, subsystem timing, allocation and GC diagnostics. Physics
calibration is active in Phase 8. [Repeatable baseline runs](docs/calibration.md)
and a [first-playable evidence checklist](phases/08-progress.md) are available;
published polar calibration and the desktop performance milestone remain open.

The upcoming Hunter 23.5 expansion is split into two phases after Phase 8:
[Phase 9: physics, validation and performance](phases/09-hunter-235-physics-and-validation.md)
researches trustworthy performance data, builds hull/two-sail physics and
verifies realism and timing in a minimal Godot scene;
[Phase 10: playable integration](phases/10-hunter-235-playable-integration.md)
adds boat selection, controls, sailing cues and cameras. Accurate physics,
fixed 60 Hz simulation cadence and stable 60 FPS presentation take priority
in both phases. The agreed Hunter scope includes separate port/starboard jib
sheets, full mainsail and jib, full fixed ballast, an adjustable centerboard,
and a skipper who can move between cockpit seats. Players will choose a boat
before sailing and can switch through F1, restarting the session.

Launch the game directly without opening the Godot editor:

```sh
./scripts/run-game.sh
```

The executable launcher builds the C# game incrementally, prepares imported
resources on first launch, then opens the game. It works from any working
directory and uses the local Godot 4.7 .NET installation or a Godot executable
on `PATH`. To use another installation, set
`GODOT_BIN=/path/to/Godot_v4.7-stable_mono_linux.x86_64 ./scripts/run-game.sh`.
The .NET 8 SDK and Godot 4.7 .NET must already be installed.

Press **F2** in-game to show the controls window with all sailing and camera
bindings; press **F2** again to hide it. Sailing and camera controls remain
active while the window is open. Hold sailing keys to adjust gradually;
releasing steering centres the tiller, while trim, board and sailor position
stay where you leave them. See [launch and controls](godot/README.md) for
the full key list.

Press **F1** for wind/wave sliders, repeatable seeds and detailed development
diagnostics. Both panels leave sailing active. See [debug tools and
performance](docs/debug-tools.md) for units, measurement boundaries and review.

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
dotnet run -c Release --project tools/SimulationHarness -- --calibration-review
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
