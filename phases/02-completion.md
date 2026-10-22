# Phase 2 Completion Record

Completed: 2026-10-22

## Delivered

- A .NET 8 solution with standalone `SailingSimulator.Core`, presentation
  adapters, an offline stepping harness, and two dependency-free regression
  executables. No Godot assembly, Godot SDK, or third-party package references.
- Immutable controls, SI environment/input configuration, rigid-body state,
  and completed output snapshots. Input and output exchange use atomic
  immutable reference publication.
- Double-precision vector/quaternion math, fixed 1/60-second coupled RK4,
  body-frame Euler angular dynamics, independent force/moment evaluation,
  force-source composition, and body-point lever-arm conversion.
- Documented NED world frame, body-to-world Hamilton quaternion convention,
  world linear/body angular velocity, Godot basis conversion, and UI units
  and compass from-directions. See [the API guide](../docs/simulation-core.md).
- Explicit non-finite/invalid-state errors identifying the failing step/stage;
  failed integration preserves the last valid snapshot and simulation clock.
- Root [build/run instructions](../README.md) and a [Godot host handoff](../godot/README.md)
  that reserves playable integration for Phase 5.

## Verification

Built with installed .NET SDK 8.0.128. `global.json` selects .NET 8; the default
.NET 10 SDK encountered an MSBuild/CLR environment failure. Builds ran outside
the filesystem sandbox after sandboxed MSBuild exited during restore without
compilation diagnostics. No project dependencies were added to work around it.

```sh
dotnet build SailingSimulator.sln --artifacts-path /tmp/SailingSimulator-phase2-artifacts --verbosity minimal -m:1 /nodeReuse:false
dotnet /tmp/SailingSimulator-phase2-artifacts/bin/SimulationCoreCheck/debug/SimulationCoreCheck.dll
dotnet /tmp/SailingSimulator-phase2-artifacts/bin/CalibrationConditionCheck/debug/CalibrationConditionCheck.dll
dotnet /tmp/SailingSimulator-phase2-artifacts/bin/SimulationHarness/debug/SimulationHarness.dll --steps 600
```

Build succeeded with zero warnings and errors. A final incremental build with
`--no-restore` and rerun of the core checks also passed after tightening the
configuration magnitude validation.

The 14 core regression groups passed: analytic coasting and force/torque motion,
exponential velocity decay, RK4 stage times and frozen input, 36,000-step
quaternion rotation, 6,000-step torque-free asymmetric inertia conservation,
3,000-step exact deterministic replay with changing controls, lever arms/source
composition, Godot vector/quaternion conversions, compass/default environment
settings, invalid input/configuration, non-finite/overflow atomic failure, and
concurrent input/output exchange. The original Phase 1 fixture check passed.

The synthetic harness compared two complete 600-step sequences successfully
and reported step 600 at 10 simulation seconds, with approximately 10 m of
northward coasting translation. These are mathematical checks, not measured
boat performance or a 60 Hz wall-clock benchmark.

## Limitations and next handoff

- Phase 3 owns the ILCA force sources and mass contributors. The foundation
  uses a fixed mass and positive diagonal inertia at the centre of mass; extend
  this for the shifting sailor, non-diagonal inertia, and mass-motion effects
  as needed. No ILCA component estimates were introduced in this phase.
- No physical wind/wave forces, hull/foil/sail models, sail shape, or force
  diagnostics are fabricated. Environment settings are configuration only.
- Phase 4 owns physical wave ranges and seeded wave realization. The core
  accepts the already selected integer seed; session-level random seed choice
  remains a host responsibility.
- Phase 5 owns the actual Godot project, scene, thread lifecycle, and 60 Hz
  wall-clock scheduler. Phase 7 owns deadline/performance diagnostics. The
  offline harness deliberately makes no real-time performance claim.
- `Step` has one owner. Force models must remain pure across RK4 evaluations;
  snapshots are safe to read concurrently, but stepping itself is not a
  concurrent API. Exact replay is verified on this runtime/platform, not
  guaranteed bit-for-bit across different architectures.
- Phase 1's outstanding coefficient, polar, mass-budget, and source
  compatibility limitations still apply before physics calibration.
