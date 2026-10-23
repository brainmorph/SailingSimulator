# Debug tools and performance

Press F1 to open/close the development panel. Sailing continues, held keys
keep their state, and key-repeat does not toggle the panel. F2 remains the
controls reference. Normal practice shows no numeric instruments. Worker
faults and deadline warnings are in F1.

## Environment

The Environment tab edits wind speed (0–30 knots, initially 10), wind-from
bearing (0–360°), wave strength (calm/gentle/moderate, initially gentle),
alignment, independent wave-from bearing and the signed integer wave seed.
The compass labels use 0/360° north, 90° east, 180° south, 270° west. Both
bearings describe where the disturbance comes **from**, not its travel direction.

Waves start aligned with wind. Unlocking their direction retains the current
bearing; subsequent wind edits leave independent waves alone. At zero wind,
the requested bearing remains in the widgets and still directs aligned waves.
The completed state reports wind as calm, since a zero velocity has no bearing.

Slider/preset changes publish a new immutable environment without changing
its seed. Enter a seed and select Apply seed to use that realization; New
random seed deliberately replaces it. Save/Restore conditions retains one
session-local set of wind/wave settings and seed. Defaults restores 10 knots,
north, gentle, aligned while keeping the seed. These controls do not rewind
boat state, trim or time. Reproducing an entire trajectory additionally
requires identical initial state and step-by-step inputs.

Settings cross the existing atomic host-input mailbox. All four RK4 stages
sample one configuration. The active-condition readout comes from the last
completed snapshot, so a pending edit is never presented as already active.
Water uses that same snapshot's spectrum, seed and time.

## Telemetry and graphs

Telemetry reads the exact `RunnerSnapshot.State` passed to boat rendering:
speed, heading, heel, pitch, leeway, apparent wind at the combined centre of
mass, position, linear/angular velocities, boom state and all eight controls.
Force breakdowns show gravity, buoyancy, hull drag, board, rudder, sail and
their sum, followed by the six sail strips' angle/twist/camber/luff values.
Forces are world north/east/down in N; moments are body forward/starboard/down
about the combined centre of mass in N m. Apparent-wind angle is a signed
body-relative from-angle; leeway is atan2(starboard velocity, forward velocity).
Wind near a rotating sail section can differ from the centre-of-mass value.

Live graphs show speed, heel, step time and deadline lateness over ten
simulation seconds. Each is limited to 600 observed snapshots, samples a
completed step only once, and uses actual snapshot timestamps. A slow render
frame can skip observations; this is not a log of every physics step. Graphs
retain history while F1 is hidden. Text and graph drawing refresh at up to
10 Hz while visible; observation still records each new rendered snapshot.
Graphs reuse a 600-point drawing buffer and a preallocated bounded queue.
Graph scales expand to the observed range; step time marks the 16.667 ms budget.

## Performance measurements

The worker always collects diagnostics, independently of panel visibility.
`RunnerSnapshot.Performance` is immutable and published with its completed
state and deadline timing. The bounded window contains up to 300 steps,
including warm-up; p50/p95/p99 use nearest rank and maximum is reported too.

- Compute duration measures held-input adjustment, fixed-step integration,
  completed-state inspection and core publication. Percentiles cover this
  work; current full step time additionally includes metrics collection and
  sorting. Deadline lateness includes scheduling delay and that metrics cost.
- The optional, single-owner core `StepProfiler` measures mass adjustment,
  four RK4 derivatives, and completed-state inspection. Hull (including local
  wave sampling), centerboard, rudder and sectional sail costs sum across all
  four stages and the final inspection. These nest inside RK4/inspection and
  must not be added to those parent timings. Timer reads add some overhead;
  no observation enters a force equation or changes simulation time.
- Worker allocations use `GC.GetAllocatedBytesForCurrentThread` between
  consecutive samples, including the preceding publication/metrics objects.
  The first sample excludes collector setup. Rolling B/s divides summed bytes
  by summed monotonic elapsed intervals, including waits. This does not
  attribute rendering allocations to the simulation worker.
- Gen0/1/2 collection counts are process-wide changes since worker start.
  Managed heap is `GC.GetTotalMemory(false)`, an approximate process-wide
  observation. Sampling does not force a collection. These counters cannot
  identify which thread triggered GC.
- Render FPS is frames divided by monotonic wall time between render callbacks over approximately one
  second; maximum frame duration is from that same interval. It is separate
  from simulation cadence. Presentation and text/graph drawing remain on the
  main thread.

Absolute 60 Hz scheduling is unchanged. Soft warning begins at three
consecutive deadline misses, Hard at five; an on-time completion resets the
consecutive count. Total misses remain visible. No catch-up, time dropping,
deadline rebasing or timestep changes conceal overload. Run wall-clock
verification separately from CPU-heavy regressions/builds; competing load
can legitimately trigger these warnings and remains a performance limitation.

## Verification

The dependency-free `tests/GodotIntegrationCheck` covers window percentile
math and eviction, allocation/GC publication, seed/bearing boundaries,
profiled versus unprofiled trajectory equivalence, deadline thresholds,
overload, threading and an isolated ten-second cadence sample.

After building the Godot project, `--smoke-test` exercises real F1/F2 events,
repeat suppression, held keys, sliders/presets/alignment, seed application,
saved-condition restoration, active state readouts, graph duplicate rejection,
metrics and warning/fault display, alongside existing rendering/lifecycle checks.

For desktop measurements, launch Godot with `-- --performance-review`.
This uses seed 123, warms up for two seconds, then samples five seconds each
with F1 hidden and each of its three tabs visible. It prints frame and compute
percentiles, allocations, GC and deadline misses. It also collects main-thread
managed allocation rate, CPU time inside boat/water rendering and overlay
updates, process GC pause duration, and process JIT compilation time. The
longest frame's GC/JIT counter deltas share its frame-start boundaries; these
are correlation evidence, not proof of a stall's cause. Render CPU timings
exclude native layout/drawing after `_Process`, GPU work and OS scheduling.
Process JIT time can include compilation on other threads.

The review preallocates its sample buffers. At each view boundary the
review retains its observations without sorting or logging. Once all timed
windows finish, it joins the worker, computes/prints statistics, and captures
four frozen PNGs at
`SailingSimulator-debug-{hidden,telemetry,environment,performance}.png` in the system
temp directory (`/tmp` on Linux, `%TEMP%` on Windows).
GPU readback cannot affect the timed samples. It exits after capture.
`PASS` means capture completed, not a promise that
the measured FPS or cadence meets the target. Judge the printed measurements
and retain hardware/renderer context; a short local sample is not universal
performance evidence.

Phase 8's [progress record](../phases/08-progress.md) retains the current
measurements and unresolved cadence/frame defects. Review output is diagnostic;
neither capture success nor reduced allocations establishes the milestone.
