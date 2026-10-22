# Phase 4 Completion Record

Completed: 2026-10-22

## Delivered

- Preserved the existing 0–30-knot wind settings (10 default), compass
  from-direction conversion and per-section apparent wind. Added independent
  verification of bearings and moving/rotating section airflow.
- Immutable seeded `DirectionalWaveField` in `EnvironmentConfig.Water`:
  24 deep-water linear components over an estimated directional spectrum,
  read-only rendering components, surface elevation/outward NED normal and
  depth-dependent orbital velocity. Sampling is pure and thread-safe.
- Calm, gentle (default) and moderate presets: spectral significant heights
  0, 0.12 and 0.30 m; non-calm wavelengths 3–12 m peaked at 6 m, with ±25°
  spread. The same phases/support scale by 2.5 between gentle/moderate.
  All preset numbers are provisional design estimates, documented with rationale
  in [the reference baseline](01-reference-baseline.md) and
  [environment equations](../docs/wind-and-waves.md).
- Hull columns and both foils sample local surface immersion and orbital flow
  at each RK4 stage. Buoyancy stays vertical; hull drag and separate foil
  lift/drag use water-relative point velocity. Wave forcing reaches all six
  degrees of motion through ordinary forces and moments.
- Wind-aligned waves retain the chosen bearing at zero wind; independent waves
  do not mutate wind. Omitted adapter seeds choose varied sessions; explicit
  integer seeds reproduce conditions. Preserve seeds when editing settings.
- `SimulationHarness --waves --wave-seed 123`, retaining existing flat-water
  `--boat` and synthetic coasting modes. Updated plan, API and host handoff;
  no third-party packages or large reference-data downloads.

## Verification

.NET SDK 8.0.128, no Godot required. Solution build succeeded with zero warnings
and errors. From the repository root:

```sh
dotnet build SailingSimulator.sln --artifacts-path /tmp/SailingSimulator-phase4-artifacts --verbosity minimal -m:1 /nodeReuse:false
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/CalibrationConditionCheck/debug/CalibrationConditionCheck.dll
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/SimulationCoreCheck/debug/SimulationCoreCheck.dll
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/BoatPhysicsCheck/debug/BoatPhysicsCheck.dll
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/EnvironmentCheck/debug/EnvironmentCheck.dll
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --steps 600
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --boat --steps 600
dotnet /tmp/SailingSimulator-phase4-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --waves --wave-seed 123 --steps 600
```

The Phase 1 fixture, all 14 Phase 2 checks and all 12 Phase 3 groups pass,
preserving calm-water behavior including ordinary-control tacks and jibes.
All eight new environment regression groups pass:

- Wind units/defaults/quadrants/limits and apparent wind with body translation,
  rotation, heel and heading.
- Extreme/negative/zero seeds, independent equal realizations, configuration
  value equality, concurrent/order-independent sampling, read-only components,
  changed-seed variation and varied session creation.
- Spectral normalization, wavelength/direction bounds and ordered strength;
  moderate surface height and orbital velocity scale by 2.5.
- Finite-difference surface normals and time derivatives matching vertical flow,
  deep attenuation, exact calm surface/velocity and invalid sample rejection.
- Independent 90° wave-field rotation with unchanged true wind, including
  aligned wave bearing at zero wind.
- Local elevation changing displacement, stationary hull/board/rudder forces
  from orbital flow, stage-time dependence, dry hull/foil zero force,
  raised-board zero force and spatially varying hull flow.
- Two independent 1,800-step trajectories compare boat, rig, mass, input,
  total force/moment and sail-section states exactly through an environment
  replacement at step 900. Gentle alone affects all six velocity components
  relative to a matched calm trajectory. Old snapshots retain their field.
  Combined gentle/moderate maximum differences in surge/sway/heave are
  1.034/0.8322/0.962 m/s and in roll/pitch/yaw rates are
  1.52/0.4315/0.4669 rad/s. These are regression evidence, not sailing targets.
- Six 3,600-step (60-second) trajectories cover gentle/moderate at 0, 10 and
  30 knots with varied wave bearings. Position/velocity/angular rate remain
  bounded and orientation remains unit length; upright sailing is not asserted.

All three 600-step harness scenarios reproduce identical states through
10 simulation seconds. The wave run publishes seed 123 for replay.
`git diff --check` passes. The final added gentle-only six-degree assertion
was verified by rebuilding and rerunning the affected `Seeded` group.

## Limitations and next handoff

- This is a provisional deep-water linear spectrum and practical sampled force
  coupling, not CFD or validated wave handling. Finite depth, breaking,
  slamming, wave radiation/diffraction, added mass and dynamic wave pressure
  are omitted. Wet columns approximate one surface intersection. Flow above
  mean level uses a bounded mean-surface extension. Initial heave balances
  instantaneous displacement at time zero.
- Config changes may jump the surface; no transition smoothing. Retain the
  existing integer seed for settings edits. Render water using the completed
  snapshot's field/time; sampling must never create a new realization.
- Phase 5 owns Godot scene/thread lifecycle, gradual held-key input and 60 Hz
  wall-clock scheduling. Phase 6 owns water visuals, Phase 7 F1 settings and
  diagnostics, Phase 8 numerical/sailor calibration. These offline runs do not
  establish deadline cadence, frame rate, speed polars or validated handling.
- Existing provisional boat coefficients/masses, unresolved 58 kg interpretation
  and published target compatibility remain open.
