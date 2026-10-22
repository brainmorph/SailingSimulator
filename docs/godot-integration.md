# Godot integration and thread contract

Phase 5 connects the standalone physics to a Godot 4.7 C# host. Godot renders
the boat; there are no physics bodies or Godot force/integration calls.

## Ownership and handoff

`SailingScene` owns engine input, nodes, resources and camera updates on the
main thread. Only that thread calls Godot APIs. This follows Godot's
[active scene tree threading restriction](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html).
`SimulationRunner` in `SailingSimulator.Adapters` contains no Godot reference.
It takes exclusive ownership of a solver and runs it on one dedicated named
background `Thread`. Do not call `Simulation.Step` externally after transfer.

Each render callback polls the fixed input actions and publishes one immutable
`HostInput` through `SnapshotMailbox<T>` (atomic reference exchange). This
contains signed held-key directions and an immutable environment realization.
The worker reads it exactly once per step, applies `HeldSailingInput.Advance`
using exactly `Simulation.StepSeconds`, then supplies one `SimulationInput`
to all RK4 stages. Holding a key adjusts controls at the same rate regardless
of render frequency. A render stall leaves the last published keys held;
focus loss explicitly publishes released keys immediately. No input queue
replays old events after a slow frame. Keep the environment's seed when
adding settings controls in Phase 7.

The worker atomically publishes one immutable `RunnerSnapshot` after each
completed step. It contains the `SimulationState` and timing for that step.
The renderer reads once per frame and uses that single snapshot throughout;
old snapshots remain valid. It renders the latest state directly, with no
interpolation. Future interpolation can retain pairs of these snapshots
without changing solver ownership.

## Frames and render origin

The core remains double-precision north/east/down, body axes
forward/starboard/down, with body-to-world quaternions. Only
`PresentationConversions` converts to Godot's −Z forward, +X starboard,
+Y up coordinates. The host copies those results into Godot float types.

Simulation position is the combined centre of mass. Meshes use the hull
datum, so the rendered origin is:

`hullWorld = state.PositionWorldM − state.OrientationBodyToWorld.Rotate(state.Mass.CenterOfMassBodyM)`

Convert that position and the orientation at the boundary. Without this
offset, hiking and board changes would visually move the hull incorrectly.
The boom reads `state.Rig.BoomAngleRad`; its Godot Y rotation is positive for
positive core boom angle, matching the aft/starboard aerodynamic chord.
Phase 6 uses the same snapshot's sail sections and water/time for the
[visual cues and cameras](visual-feedback.md).

## Wall-clock cadence

`SimulationRunner` uses `Stopwatch` monotonic timestamps, with absolute
deadlines at `epoch + round(stepIndex × frequency / 60)`. The first step
begins immediately and is due one period later. A late step finishes after
its deadline, including any scheduling delay before its start. The clock
never depends on render delta or Godot physics ticks. A cancellable wait
sleeps for most of each interval and spins briefly near the target time.

`FixedStepSchedule` preserves the original deadline grid and enforces at
least one period between actual starts. It does not run shortened intervals
to catch up, skip steps, reset deadlines or enlarge the physics timestep.
If execution or OS scheduling exceeds the budget, wall-clock lateness can
therefore accumulate. This is reported as a performance defect; the runner
does not conceal it by rebasing time. Simulation time still advances by
exactly 1/60 s for every completed step. This is a monitored desktop schedule,
not an operating-system real-time guarantee.

`StepTiming` reports step duration (input adjustment plus integration and
diagnostics), elapsed wall time, deadline lateness, consecutive misses and
total misses. A successful deadline resets the consecutive count. Three
misses mean Soft; five mean Hard. The temporary status label displays those
warnings and worker faults. Phase 7 will place them in F1 and extend these
metrics with rolling percentiles, subsystem costs, allocations and GC.

## Lifecycle and failure

Each runner is single-use. `Start` rejects a second start or a stopped/disposed
session. `Stop` signals the wait and joins the worker, finishing any step
already in progress. `Dispose` calls `Stop` and releases the wait resource;
both are idempotent and safe for concurrent lifecycle callers. `_ExitTree`
disposes the runner before Godot nodes disappear. A fresh scene creates a
fresh runner; there is no restart of an old worker.

A worker exception retains the last completed state, publishes its fault and
sets `Running` false. No partially integrated state is rendered. The main
thread displays the failure without accessing engine objects from the worker.
The solver's stage/step failure context remains available for diagnosis.

## Evidence and limits

[GodotIntegrationCheck](../tests/GodotIntegrationCheck/Program.cs) verifies
rates/bounds/release, deadline arithmetic, concurrent paired host inputs,
one RK4 input sample, completed render reads, error publication, repeated
and concurrent shutdown, overload warnings and a ten-second ILCA cadence
with no render callbacks. The real Godot scene also has `--smoke-test`:
see [launch and controls](../godot/README.md).

Headless checks verify scene execution and transforms. Phase 6 also provides
rendered fixtures and a visual review checklist; these do not establish
desktop render FPS or sailor validation. Phase 7 owns full performance
diagnostics; Phase 8 owns physics calibration.
