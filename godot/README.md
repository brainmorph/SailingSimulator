# Godot host

Run `./scripts/run-game.sh` from the repository root to build and launch the game
directly. The script also prepares imported resources on first launch, so
you do not need to open the editor. Set `GODOT_BIN` to override the detected
Godot 4.7 .NET executable.

Open `project.godot` with **Godot 4.7 .NET (Mono)** and build the C# project,
then run the main scene (F6) or project (F5). The local executable is recorded
in [LOCAL_ENVIRONMENT.md](../LOCAL_ENVIRONMENT.md). From the repository root:

```sh
dotnet build godot/SailingSimulator.Godot.csproj
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot
```

The Godot SDK and bindings are the only engine dependencies; no third-party
packages are added. The root solution deliberately remains buildable with
only .NET 8; build the Godot project separately as shown above.

The test scene starts an ILCA 7 on a beam reach in the default 10-knot
northerly wind and gentle waves. Each session chooses a wave seed. The
physics coefficients are provisional. Low-fidelity hull, rig, sailor and foil
meshes follow the simulation. Sail strips show its aerodynamic camber, twist
and luffing; paired red/green telltales respond to local airflow. Water uses
the same seeded wave spectrum and time as physics, with a fading motion-based
wake. F1 opens environment settings, live telemetry/graphs and performance
diagnostics; see [measurement details](../docs/debug-tools.md).

## Fixed controls

Hold a key to adjust. Both keys in a pair cancel. Physical key positions are
used, independent of keyboard text layout.
Press **F2** to show the in-game controls window; press **F2** again to hide
it. Sailing and camera controls continue working while it is open.

| Keys | Action | Rate (normalized units/second) |
| --- | --- | ---: |
| A / D | Tiller port / starboard (turn direction in forward flow) | 1.5 |
| W / S | Mainsheet in / out | 0.25 |
| Q / E | Traveler in / out | 0.25 |
| R / F | Vang on / off | 0.25 |
| T / G | Outhaul on / off | 0.25 |
| U / J | Cunningham on / off | 0.25 |
| I / K | Centerboard down / up | 0.25 |
| Z / X | Sailor toward port / starboard | 0.5 |
| C | Switch third-person / onboard camera | — |
| Right mouse drag | Orbit in third-person; look around onboard | — |
| Mouse wheel | Third-person distance; onboard field of view | — |
| F1 | Show / hide debug telemetry, environment and performance | — |
| F2 | Show / hide controls window | — |

Tiller returns to centre at 1.5 units/second when released. Other settings,
including hiking, stay at their last value. Losing application focus clears
held keys immediately and releases mouse orbit; simulation continues and
tiller centres. Releasing keys never resets trim. Initial normalized settings:
tiller 0, sheet 0.6, traveler 0.8, vang 0.6, outhaul 0.5, cunningham 0.3,
centerboard 1, hiking −0.3. These rates/settings are provisional usability
choices, not measured ILCA trim values. Stop/restart the project for a new
session; capsize recovery is deferred.

Third-person follows the boat's heading with a level horizon and targets the
whole rig. Onboard follows the hiking position and boat roll/pitch; the sailor
proxy is hidden to keep the camera clear. Each view retains its own look/zoom.
See [visual feedback and review](../docs/visual-feedback.md) for cue meanings,
presentation estimates and a short review checklist.

## Verification

Run the dependency-free handoff/scheduler/lifecycle regression checks:

```sh
dotnet run --project tests/GodotIntegrationCheck
```

After building the Godot project, verify the actual scene without a display:

```sh
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --editor --import --quit
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot -- --smoke-test
```

The smoke test uses seed 123, checks all physical key pairs and opposing
keys, compares rendered position/orientation with the completed simulation
state, deliberately stalls the main thread, tests focus loss and mouse
isolation in both cameras, checks shared sail/wave data and body/foil
positions, verifies F2 show/hide and key-repeat handling, and joins the
worker before exiting. A pass prints `PASS Godot
scene` and exits 0. See [the integration contract](../docs/godot-integration.md)
for ownership, cadence and failure behavior.

The scene smoke also exercises F1 events, repeat suppression, environment
sliders, independent waves, seed application and saved conditions, completed
snapshot telemetry, graph sampling and performance/warning/fault display.
F1 settings retain the seed while editing wind or waves. Apply seed and
New random seed explicitly select another realization. Save/Restore conditions
stores one session-local environment; it does not rewind the boat or time.

For a 22-second live desktop frame/worker measurement with F1 hidden and each
tab visible, including screenshots in `/tmp/SailingSimulator-debug-*.png`:

```sh
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot -- --performance-review
```

For repeatable rendered review (requires a display), run:

```sh
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot -- --visual-review
```

This opens a brief window, saves eleven PNGs (including the controls window) to
`/tmp/SailingSimulator-<fixture>.png`, then exits. It uses real aerodynamic
diagnostics in frozen instantaneous fixtures, not calibrated steady sailing
or scripted maneuver physics. It does not start a simulation worker.

For offline Phase 8 comparisons, see the [calibration baseline protocol](../docs/calibration.md).
The baseline suite records simulation conditions and assembly hashes and
rejects unsettled free-sailing cases as steady comparison candidates. Its
results are internal model observations; published-data calibration and
desktop timing remain open in the [Phase 8 progress record](../phases/08-progress.md).
