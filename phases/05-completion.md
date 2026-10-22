# Phase 5 Completion Record

Completed: 2026-10-22

## Delivered

- Godot 4.7 .NET project and runnable main scene referencing the standalone
  core/adapters. Simple hull, mast, sail/boom and mean-water proxies show
  solver-driven motion with a mouse orbit camera. No Godot physics bodies
  or engine-driven sailing dynamics; no third-party packages added.
- `SimulationRunner` owns a dedicated C# thread and monotonic absolute 60 Hz
  deadlines. No render delta, shortened catch-up intervals, skipped steps,
  reset deadlines or enlarged timestep. Basic timings, consecutive/total
  misses and Soft/Hard warnings at 3/5 misses are published with each step.
- Immutable `HostInput` publication pairs held-key directions and environment.
  The worker samples once per step, ramps all eight bounded controls at fixed
  rates and passes one input through RK4. The main thread reads one completed
  `RunnerSnapshot` for the frame. Trim/hiking persist; released tiller centres.
- Fixed physical keys: A/D tiller, W/S sheet, Q/E traveler, R/F vang, T/G
  outhaul, U/J cunningham, I/K board, Z/X hiking. Opposing keys cancel.
  Right mouse drag orbits; wheel changes distance. Focus loss immediately
  clears held keys and orbit, while the simulation continues.
- NED/quaternion conversion stays at the adapter boundary. Rendering offsets
  the published combined COM to the hull datum, including moving sailor/board
  mass. The boom proxy reads the published rig angle.
- Single-use start, idempotent stop/dispose, cancellable waits and joined
  `_ExitTree` shutdown. Worker faults retain the last completed state and
  appear in a temporary status label. No worker touches Godot objects.
- [Controls and launch commands](../godot/README.md),
  [thread/scheduling contract](../docs/godot-integration.md), updated plan,
  and a dependency-free integration check project in the root solution.
  The engine host builds separately to preserve .NET-only foundation builds.

## Verification

.NET SDK 8.0.128; Godot 4.7 stable Mono (`5b4e0cb0f`), using the existing
local SDK cache and engine installation. Both solution and Godot builds
succeeded with zero warnings/errors. Commands from the repository root:

```sh
dotnet build SailingSimulator.sln --verbosity minimal -m:1 /nodeReuse:false
dotnet build godot/SailingSimulator.Godot.csproj --verbosity minimal -m:1 /nodeReuse:false
dotnet tests/CalibrationConditionCheck/bin/Debug/net8.0/CalibrationConditionCheck.dll
dotnet tests/SimulationCoreCheck/bin/Debug/net8.0/SimulationCoreCheck.dll
dotnet tests/BoatPhysicsCheck/bin/Debug/net8.0/BoatPhysicsCheck.dll
dotnet tests/EnvironmentCheck/bin/Debug/net8.0/EnvironmentCheck.dll
dotnet tests/GodotIntegrationCheck/bin/Debug/net8.0/GodotIntegrationCheck.dll
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --editor --import --quit --log-file /tmp/SailingSimulator-phase5-import.log
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --log-file /tmp/SailingSimulator-phase5-smoke.log -- --smoke-test
git diff --check
```

The Phase 1 fixture, 14 core checks, 12 boat groups and eight environment
groups all pass. Six new integration groups pass:

- All held-control rates/bounds, released tiller, retained trim/body position
  and invalid input rejection.
- Deterministic deadline arithmetic, no shortened catch-up interval, no
  deadline reset/skip after overload and fractional-frequency deadline grid.
- Concurrent paired keys/environment publication, coherent step sampling
  through all RK4 stages, dedicated worker identity, monotonic completed
  state/timing reads, and historical snapshot retention.
- Atomic fault publication and last valid state, repeated/concurrent stop,
  repeated dispose and rejection of invalid restarts.
- Deliberately slow forces produce accumulating lateness and specified
  warning thresholds while the simulation clock advances only for actual steps.
- Ten seconds without render reads advances 600 ILCA steps, with final
  completion at 9.9900 wall-clock seconds (the first step starts immediately).
  Zero late steps and no warning; final step cost 5.848 ms. This is one local
  cadence sample, not a hardware-wide performance guarantee.

The actual Godot headless scene passes with 91 completed steps: all 16
physical keys and opposing pairs, held-key ramps, rendered hull datum and
orientation, monotonic snapshots, continued stepping through a deliberate
250 ms main-thread stall, focus-loss release/centring, actual mouse orbit
without sailing-input changes, and joined worker shutdown. The engine exits
0 with `PASS Godot scene` and no engine errors. Synthetic key checks flush
Godot's buffered input events before checking action state.

The initial sandboxed engine import could not create user data or editor
sockets/settings; the approved headless import and smoke runs succeeded
outside that restriction. No downloaded data or dependency workaround.

## Limitations and next handoff

- The scene is an integration test presentation. Water is a flat mean-level
  plane even though physics samples gentle waves. The sail is a triangular
  proxy, not the final shared sectional mesh. Phase 6 owns sail shape,
  luffing/telltales, visible wave motion, body/foil detail and onboard plus
  third-person cameras. Visual usability and desktop render FPS have not
  been established by headless tests.
- Phase 7 owns F1 controls/graphs, environment editing and full timing,
  allocation and GC instrumentation. Use the published state/environment
  and preserve its seed. Move the temporary warning/fault display into F1.
- Adjustment rates and starting trim are provisional usability choices.
  Existing mass/coefficient/wave estimates, capsize behavior and missing
  measured performance targets remain Phase 8 calibration limitations.
- Desktop scheduling can miss deadlines. Original deadlines stay anchored
  and starts never accelerate to recover lost time, so overload or scheduling
  delay can accumulate lateness; diagnose it rather than adding a hidden
  slowdown, catch-up or dropped-time policy.

Next active phase: [Visual sailing feedback and cameras](06-visual-sailing-feedback-and-cameras.md).
