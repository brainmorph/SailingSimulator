# Godot host

Run `./scripts/run-game.sh` from the repository root to build and launch the game
directly. The script also prepares imported resources on first launch, so
you do not need to open the editor. Set `GODOT_BIN` to override the detected
Godot 4.7 .NET executable.

The game opens with a boat picker: ILCA 7 or Hunter 23.5. No simulation runs
until a boat is chosen. Skip the picker with `./scripts/run-game.sh --
--boat=ilca-7` or `-- --boat=hunter-235`. F1 · Session changes boats; this
restarts the session, keeping the current wind, waves and seed and resetting
time, boat motion, sails and all sailing controls, held keys, graphs and wake.
The Hunter adds independent jib-sheet keys (below). It still renders
provisional block geometry with ILCA camera framing; Phase 10.3 adds its
recognizable geometry, sail cues and fitted cameras.

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

For the provisional Hunter development host, launch `./scripts/run-game.sh -- --hunter-dev`.
It runs the dedicated simulation worker with fixed initial controls, 10-knot
wind and calm water, and renders simple geometry from completed snapshots.
F1 toggles development telemetry, including sail forces, sheet positions and
worker timing. The flat water and block hull are placeholders; this is not the
playable Hunter interface or a performance acceptance measurement. For an
automated headless worker and geometry check, use `-- --hunter-smoke` instead.

Run `./scripts/run-game.sh -- --hunter-scenario` for a scripted worker exercise:
four seconds of fixed controls followed by steering toward the wind. After 20 s,
the starboard jib sheet eases and the port sheet trims for three seconds while
steering continues until 44 s. After ten seconds with the tiller released, the
board and crew move in opposite directions for four seconds each. Stage
transitions and heading are printed to the console.
`-- --hunter-scenario-check` exits after the sequence and checks that the sheet,
board and crew controls moved. The script is driven by completed simulation
time, but input handoffs occur on render frames, so exact step boundaries can
vary. The shared sequence also has an exact-step offline replay that checks
crossing the upwind heading, both sails crossing and subsequent filling and
speed recovery; the host check does not independently verify a tack.

`-- --hunter-steady-check` runs a 120-second worker-host reach in the same
environment. A small scripted helm correction maintains a tiller setting near
-0.07 through the held-input interface; other controls remain at their initial
settings. Because the heading converges slowly, it reports speed, heading, heel
and sail luffing over the final 20 seconds and checks a bounded, upright,
filled-sail reach. The offline
`dotnet run --project tests/BoatPhysicsCheck -- "Hunter steady reach"` replay
uses a fixed -0.07 tiller and tighter settling limits. Render-thread input
handoffs make the host run approximate, and neither run is empirical validation
or desktop performance evidence.

For an early desktop measurement of the Hunter scene, run
`./scripts/run-game.sh -- --hunter-performance-review`. After a two-second
warm-up it records ten seconds each with telemetry hidden and visible, then
stops the worker, prints the results and saves a frozen scene to
`/tmp/SailingSimulator-hunter-review.png`. Frame percentiles cover each full
window; worker compute percentiles and allocation rate are the worker's rolling
300-step window, and GC counts are cumulative from worker start. Step counts,
deadline misses and start overrun are window differences. This uses the fixed
development inputs, not the steady-reach helm scenario. The short measurements
are diagnostic and do not by themselves establish the performance gate.
GC pause and JIT deltas are process-wide; the maximum-frame deltas share the
frame measurement boundaries and show correlation rather than causation.
Render and telemetry CPU timings cover managed scene-update calls and exclude
native layout/drawing, GPU work and scheduling outside those calls.
The reported process time covers the beginning of `_Process` through the
review's sampling point. The maximum-frame outside-process gap runs from the
preceding sampling point to the next `_Process` entry; it also includes the
small unsampled callback tail, native work, waits and scheduling.
The review also reports a worker warm-up snapshot, lag at both window
boundaries, and whether the application lost focus or was unfocused at the
start of each window. Treat unfocused windows as separate observations: desktop
focus can change rendering behavior and makes them unsuitable for the normal
focused-window comparison.

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
| Z / X | Sailor toward port / starboard (Hunter: skipper between cockpit seats) | 0.5 |
| O / L | Hunter port jib sheet trim / ease | 0.25 |
| P / ; | Hunter starboard jib sheet trim / ease | 0.25 |
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
centerboard 1, hiking −0.3. The Hunter starts with main 0.7, starboard
(leeward) jib sheet 0.7, port jib sheet 0 and the skipper on the port seat.
The jib-sheet keys do nothing on the ILCA. Nothing eases or trims a sheet
automatically: when tacking or jibing the Hunter, ease the old jib sheet and
trim the new one. F2 lists only the selected boat's controls. These
rates/settings are provisional usability choices, not measured trim values.
Restart through F1 · Session; capsize recovery is deferred.

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

The key check includes both jib-sheet pairs. The smoke starts with the ILCA
and then restarts through F1 · Session to the Hunter, back to the ILCA and to
the Hunter again. Each restart must join the
previous worker, release held keys, clear graphs and wake, show only the
selected boat, retain the environment and seed, reset controls and time, and
render and display the new session. After each restart it checks that F2
lists that boat's controls, that held jib-sheet keys ramp gradually on the
Hunter and leave the ILCA unchanged, and that focus loss releases them.
`-- --picker-check` verifies that the picker holds the session until a
choice, then that choosing the Hunter starts and steps its session.

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

This opens a brief window, saves twelve PNGs (including the controls window for each boat) to
`/tmp/SailingSimulator-<fixture>.png`, then exits. It uses real aerodynamic
diagnostics in frozen instantaneous fixtures, not calibrated steady sailing
or scripted maneuver physics. It does not start a simulation worker.

For offline Phase 8 comparisons, see the [calibration baseline protocol](../docs/calibration.md).
The baseline suite records simulation conditions and assembly hashes and
rejects unsettled free-sailing cases as steady comparison candidates. Its
results are internal model observations; published-data calibration and
desktop timing remain open in the [Phase 8 progress record](../phases/08-progress.md).
