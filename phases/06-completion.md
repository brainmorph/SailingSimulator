# Phase 6 Completion Record

Completed: 2026-10-22

## Delivered

- Low-fidelity procedural hull/cockpit, rig, sail, board, rudder and sailor.
  Hull attitude reads the completed six-degree-of-freedom state. Board and
  sailor centres follow the mass contributors; rudder follows the foil angle.
- Sectional sail triangles use the six published aerodynamic strips' chord,
  angle, camber, height and local wind. Foot/head extend those strips; section
  seams and the leech reveal twist. Shared model dimensions replace duplicated
  render estimates. The old boom proxy's rotation sign and mast position are
  corrected to match the aerodynamic chord and mast attachment.
- Deterministic wind-gated rippling uses the same `Luffing` that reduces sail
  forces. Paired red/green telltales follow local flow, reverse, droop in still
  air and break up with luffing/high incidence. Still-air ribbons retain
  visible triangle area. Animation/separation thresholds are documented
  visual estimates, not a new force or cloth model.
- GPU wave displacement/normals use all physical spectrum components, seed
  phases, world coordinates and snapshot time. An indexed 16,641-vertex mesh
  is dense near the boat and coarse at the horizon. Height/slope shading
  reveals gentle waves; distant optical detail fades. Calm is flat.
- A bounded 80-sample, seven-second wake follows stern motion relative to
  local water, spreads/fades on the physical wave surface, and stops spawning
  below 0.25 m/s. Render reads cannot generate extra history.
- C switches between a heading-following third-person orbit and an onboard
  camera following body attitude/hiking. Each retains look/zoom. Right drag,
  wheel and focus loss leave sailing inputs isolated. Normal play has visual
  cues and a small control hint, with no numeric instruments.
- [Model and review checklist](../docs/visual-feedback.md), updated
  [launch/controls](../godot/README.md), and `--visual-review` raster fixtures.
  No third-party dependencies, downloaded data or physics retuning.

## Verification

.NET SDK 8.0.128; Godot 4.7 stable Mono (`5b4e0cb0f`). Root solution and Godot
project build with zero warnings/errors. Commands from the repository root:

```sh
dotnet build SailingSimulator.sln --verbosity minimal -m:1 /nodeReuse:false
dotnet build godot/SailingSimulator.Godot.csproj --verbosity minimal -m:1 /nodeReuse:false
dotnet tests/CalibrationConditionCheck/bin/Debug/net8.0/CalibrationConditionCheck.dll
dotnet tests/SimulationCoreCheck/bin/Debug/net8.0/SimulationCoreCheck.dll
dotnet tests/BoatPhysicsCheck/bin/Debug/net8.0/BoatPhysicsCheck.dll
dotnet tests/EnvironmentCheck/bin/Debug/net8.0/EnvironmentCheck.dll
dotnet tests/GodotIntegrationCheck/bin/Debug/net8.0/GodotIntegrationCheck.dll
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --editor --import --quit --log-file /tmp/SailingSimulator-phase6-import.log
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --log-file /tmp/SailingSimulator-phase6-smoke.log -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot --log-file /tmp/SailingSimulator-phase6-render-smoke.log -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot --log-file /tmp/SailingSimulator-phase6-visual.log -- --visual-review
git diff --check
```

The calibration fixture, 14 core checks, 12 boat groups and eight environment
groups pass, including ordinary-control tack/jibe regression coverage.
All ten integration groups pass. Four added groups verify:

- Render geometry preserves section chords/angles, trim visibly changes
  camber/twist, luff flutter accompanies reduced force, and still air is static.
- Telltales stream, reverse, hang and break up with local incidence/airflow.
- Shader spectral inputs reproduce physical elevations/normals across all
  presets, several compass directions, positions and times.
- Wake motion threshold, transverse direction, age/capacity, duplicate reads
  and complete decay after stopping.

The ten-second worker check completed 600 steps in 9.9875 wall-clock seconds,
with zero late steps and a final 3.621 ms step. This is a local cadence sample,
not a hardware-wide guarantee or a desktop render-FPS measurement.

The actual scene passes headless (91 completed steps in the final run) and
with the Compatibility renderer (92 steps). Extended smoke checks compare
every sail strip to its source geometry, verify boom side, body/board centres,
wave count/time, both cameras, mouse isolation and still-air ribbon area.
Existing held-key, focus, deliberate render stall and joined-shutdown checks
remain. Both runs exit 0 and print `PASS Godot scene`.

Ten 1280×720 review images are generated in `/tmp/SailingSimulator-*.png`.
Raster inspection covered reaching on either side, luffing at two instants,
both onboard sides, heel/wake, downwind boom on either side and calm/stopped
with a raised board. Camera framing and wave contrast were adjusted after
inspection. The local renderer used Intel HD Graphics 530 after the system
NVIDIA driver fallback. Import/runtime verification used approved engine
access outside sandbox settings/socket/display restrictions.

## Limits and next handoff

- The review fixtures prescribe instantaneous body/rig states and inspect
  real force diagnostics. They do not establish calibrated steady sailing,
  maneuver quality, human usability or sailor approval. Follow the short
  sailing checklist during Phase 8 feedback/calibration.
- Sailcloth/ribbons/wake and proxy hull/foil geometry are deliberately modest.
  Continuous physical waves are approximated between rendered vertices;
  coarse distant detail fades. Water is opaque. No capsize recovery.
- Phase 7 owns F1 controls/graphs and timing percentiles, subsystem timings,
  allocation/GC diagnostics and desktop render-FPS assessment. Move the
  temporary warning/fault label into F1. Keep the active wave seed when editing
  settings; water visuals update automatically from the next completed state.
- Existing mass interpretations, aerodynamic/hydrodynamic coefficients,
  wave-preset estimates and missing verified polar targets remain calibration
  limitations. The core equations are unchanged; sail dimensions are exposed
  as shared constants.

Next active phase: [Debug tools and performance](07-debug-tools-and-performance.md).
