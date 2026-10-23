# Phase 3 Completion Record

Completed: 2026-10-22

## Delivered

- A standalone still-water `IlcaBoatModel`: gravity, 48 sampled buoyancy/drag
  columns, separate centerboard/rudder lift and drag, and six sail sections.
  All eight controls affect mass geometry, force, sail shape or boom response.
- Separate hull, rig, centerboard, rudder, fittings and sailor contributors:
  provisional equipped mass totals 58 kg; the separate 80 kg sailor gives
  138 kg combined. Full inertia tensors include parallel-axis terms and
  products of inertia. Hiking shifts the sailor point continuously.
- RK4 now integrates boom angle/rate together with all six rigid-body degrees
  of freedom. Aerodynamic pressure and compliant sheet tension drive boom
  crossing; no maneuver action or heading/speed target exists in the model.
- Completed immutable snapshots publish mass, rig, six independent force/moment
  contributions and read-only section shape, local airflow, luffing and force.
  Rendering can use the same sectional representation as aerodynamics.
- `SimulationHarness --boat` provides a deterministic, inspectable flat-water
  run. The original synthetic coasting mode is preserved.
- [Model equations and estimates](../docs/boat-physics.md), updated core API,
  control signs, root instructions and plan. A small current-class-rule review
  left the 58 kg removable-equipment interpretation unresolved; no bulk data
  or thesis download was performed.

## Verification

.NET SDK 8.0.128, without third-party packages or Godot. Build succeeded with
zero warnings and errors. Commands from the repository root:

```sh
dotnet build SailingSimulator.sln --artifacts-path /tmp/SailingSimulator-phase3-artifacts --verbosity minimal -m:1 /nodeReuse:false
dotnet /tmp/SailingSimulator-phase3-artifacts/bin/CalibrationConditionCheck/debug/CalibrationConditionCheck.dll
dotnet /tmp/SailingSimulator-phase3-artifacts/bin/SimulationCoreCheck/debug/SimulationCoreCheck.dll
dotnet /tmp/SailingSimulator-phase3-artifacts/bin/BoatPhysicsCheck/debug/BoatPhysicsCheck.dll
dotnet /tmp/SailingSimulator-phase3-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --steps 600
dotnet /tmp/SailingSimulator-phase3-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --boat --steps 600
```

The Phase 1 fixture and all 14 Phase 2 regression groups pass. All 12 new
`BoatPhysicsCheck` groups pass:

- Exact mass budget, moving contributors, tensor validation and inverse solve.
- 6,000 torque-free full-tensor steps conserving world angular momentum and
  rotational energy, and 120 steps of internal mass movement conserving momentum.
- Analytic boom acceleration integrated through RK4; rig-stage and diagnostic
  completion failures preserve the last complete snapshot.
- Hydrostatic balance, heave/roll/pitch restoring, hiking signs and no dry
  hull/foil water forces; passive hull/foil power including rotational flow.
- Leeway opposition, raised-board zero force and small/finite steering response.
- All trim controls, correct 7.06 m² sectional area, shared shape/force state,
  apparent wind, and luffing with reduced force.
- Continuous boom crossing with reversed airflow; 1,800-step changing-control
  deterministic replay, read-only old snapshots and motion in all six degrees.
- Upright tack and jibe from ordinary steering, gradual hiking and sheeting
  inputs in 10-knot north wind, starting at 3 m/s. Tack crosses head-to-wind
  and boom at 19.55 simulation seconds; jibe crosses downwind and boom at
  22.73 seconds. These timings are regression evidence, not measured targets.
- Eight 7,200-step (120-second) cases at 0, 10 and 30 knots, including mirrored
  headings, all-up/all-down board and extreme tiller/trim/hiking. Each preserves
  unit orientation and bounded position/velocity. Calm rest settles. Poor
  hiking or strong wind can overturn the model; these tests do not claim every
  input is upright or sail-able.

Both 600-step harness modes reproduce identical trajectories through 10
simulation seconds. These offline checks do not establish wall-clock 60 Hz
cadence, rendering frame rate, or measured ILCA speed/polar fidelity.

## Limitations and next handoff

- Component masses, centres, inertia, hull rocker/waterplane, foil dimensions,
  sail geometry and coefficients are explicit provisional estimates. Published
  VPP targets, coefficient tables, crew compatibility and the equipped-mass
  interpretation still need verification before calibration.
- Internal mass movement is quasi-static between steps. Crew transit dynamics,
  boom/hull inertial reaction coupling, flooding, capsize recovery, planing,
  wave-making resistance and unsteady aeroelasticity are not modeled.
- Phase 4 owns seeded wave realization and physical presets. Replace the
  flat z=0 / stationary water assumption at hull/foil samples with stage-time
  surface elevation and local flow; preserve the still-water regression cases.
  Apparent wind already includes local boat/boom velocity.
- Phase 5 owns gradual held-key input, actual Godot/thread lifecycle and
  wall-clock scheduling. Phase 6 owns sail/telltale rendering. Phase 7 owns
  timing/allocation diagnostics; new section snapshots intentionally allocate.
  Phase 8 owns speed/pointing calibration and reproducible handling checks.
