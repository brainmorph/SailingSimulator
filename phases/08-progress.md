# Phase 8 progress

Started: 2026-10-23. **Phase 8 and the first-playable milestone remain open.**
This pass establishes reproducible baselines, reduces debug graph allocations
and improves performance attribution. It does not calibrate physics parameters
or establish empirical ILCA realism.

## Scope revision — 2026-10-27

Phase 8 is now [ILCA engineering readiness](08-calibration-and-first-playable.md).
The first-playable milestone is a prototype with provisional physics.
Objective-data comparisons and measurement-based tuning have moved to
[Phase 11](11-calibration-and-realism-validation.md) for both boats; missing
compatible measurements no longer block Phases 8–10. Existing results below
are retained. No physics or performance work was performed by this roadmap
revision, and it does not complete Phase 8: review tooling, dynamic
handling/visual checks and the desktop timing defect remain engineering work.

## Delivered and observed

- `SimulationHarness --calibration-review` runs seven explicit 30-second
  cases with independent, exact per-step replays. The [protocol and initial
  results](../docs/calibration.md) explain the screening criteria and limitations.
  [Recorded JSON](08-baseline-results.json) retains inputs, initial/final state,
  one-second traces, final-window statistics, runtime/build metadata and assembly
  hashes. The original Phase 1 example condition remains an unmodified example.
- All seven cases replay exactly. Coasting slows from 3 to 0.223 m/s. Only the
  nearly stopped head-to-wind case passes the steady screen. Other cases drift
  from their initial course or keep changing speed. The calm 90° case finishes
  at 79.39°, so its near-constant speed is not a valid 90° polar point.
- Graphs retain every observed completed snapshot, up to 600 over ten seconds,
  while drawing at up to 10 Hz. Preallocated queues/vertices and span-based
  polyline drawing replace per-draw LINQ/array allocations. Duplicate suppression,
  timestamps, scale, reference line and hidden-view history are preserved.
- The desktop review now reports main-thread allocations, rendering/overlay
  callback costs, process GC pause duration and process JIT time. GC/JIT deltas
  around the largest frame use matching frame-start boundaries. Statistics,
  logging and GPU readback run after timed samples and joined worker shutdown.
  This removes review/reporting work as a source of later timed-window stalls.
- The absolute 60 Hz schedule, fixed RK4 timestep, no-catch-up policy and 3/5
  warning thresholds are unchanged. No physics coefficient, mass, trim rate,
  wave estimate or source target was changed. No dependencies were added.

## Desktop evidence and remaining defect

Godot 4.7 stable Mono (`5b4e0cb0f`), Debug game build, .NET SDK 8.0.128/runtime
8.0.28, Ubuntu 26.04, Intel i7-6700HQ (4 cores/8 logical CPUs). The unavailable
NVIDIA driver again falls back to Intel HD Graphics 530, Mesa 26.0.3, OpenGL
Compatibility. These are local desktop results, not a hardware-wide guarantee.

The unmodified Phase 7 baseline reproduced persistent misses in this session:
hidden/telemetry/environment/performance had 289/300/300/299 late steps, with
an 81.95 ms maximum telemetry frame. An attribution-only run before graph
changes measured main allocations of 1,184.1 KiB/s in telemetry and 1,119.0
KiB/s in performance. It also recorded GC pauses and substantial first-use
JIT activity; those observations alone do not assign every stall a cause.

The final run below had no concurrent builds or heavy regressions. It uses
seed 123, two seconds of warm-up and five seconds per view, including tab
opening. [Raw final output](08-desktop-results.log) retains the extra counters.
Mean FPS is computed from monotonic frame intervals. Worker percentiles use
the rolling 300-step compute window; tab transitions can therefore leave a
few earlier steps in that window.

| F1 view | Mean FPS | Frame p95 / p99 / max, ms | Compute p95 / max, ms | Late steps | Consecutive at end | Main allocation, KiB/s |
| --- | ---: | --- | --- | ---: | ---: | ---: |
| Hidden | 59.95 | 16.91 / 17.12 / 17.37 | 6.440 / 9.008 | 0 | 0 | 367.3 |
| Telemetry | 59.15 | 18.96 / 26.22 / 54.72 | 10.093 / 19.486 | 11 | 0 | 611.8 |
| Environment | 59.95 | 17.09 / 19.23 / 23.88 | 7.666 / 17.680 | 13 | 0 | 489.8 |
| Performance | 59.75 | 17.23 / 20.07 / 33.15 | 7.455 / 22.956 | 66 | 54 | 494.4 |

Visible graph allocations are substantially lower, but this **does not meet
the sustained desktop performance criterion**. The final performance view
ends with a hard warning and 7.19 ms lateness. Another graph-only run had two
hidden-view misses and none in the other views, yet retained a 61.85 ms
telemetry frame; the variation reinforces the need for further diagnosis.

The final telemetry window has 172.85 ms of process JIT activity and 5.01 ms
of process GC pause time across five seconds. However, neither counter
advances across its largest 54.72 ms frame. Callback wall time also excludes
native UI layout/drawing, GPU work and scheduling outside `_Process`. Further
profiling must distinguish those costs and worker descheduling from actual
force-computation cost. It would be incorrect to attribute the whole defect
to GC or claim that graph optimization fixed it.

Frozen captures remain at `/tmp/SailingSimulator-debug-*.png`. Raster review
of the performance capture confirms readable graph traces/scales and the hard
warning. Captures occur after joined worker shutdown; their displayed FPS
includes reporting/readback and must not replace the table's timed values.

## Verification

The root solution, Godot project and Release harness build with zero warnings
and errors. All existing regression groups pass: 14 core, 12 boat, eight
environment and 14 integration groups, plus the original condition contract
and new calibration screening/replay checks.

New checks cover circular heading wrap, oscillating speed with equal means,
excessive heel rejection, exact coasting replay/dissipation, final-window time
boundaries/sample counts, a partial final trace second, and repeated seeded
gentle-wave traces. The scene smoke adds graph capacity/duplicate/time-gap
eviction checks and passes both headless and with the desktop renderer.
Existing real F1/F2 events, settings, camera/sail/water consistency, render
stall independence, focus/input and joined-shutdown checks remain passing.

The isolated integration sample completed 600 steps in 9.9910 seconds, with
one late step and zero consecutive misses at the end; compute p50/p95/p99/max
was 5.034/6.633/9.115/16.201 ms. Its passing tolerance does not erase that
miss or establish zero-miss desktop cadence. Existing handling checks cross
the tack at 19.55 s and jibe at 22.73 s through ordinary controls; these are
numerical/model consistency checks, not calibrated maneuver timings.

Commands run from the repository root:

```sh
dotnet build SailingSimulator.sln --verbosity minimal -m:1 /nodeReuse:false
dotnet build godot/SailingSimulator.Godot.csproj --verbosity minimal -m:1 /nodeReuse:false
dotnet build tools/SimulationHarness/SimulationHarness.csproj -c Release --verbosity minimal -m:1 /nodeReuse:false
dotnet tests/CalibrationConditionCheck/bin/Debug/net8.0/CalibrationConditionCheck.dll
dotnet tests/SimulationCoreCheck/bin/Debug/net8.0/SimulationCoreCheck.dll
dotnet tests/BoatPhysicsCheck/bin/Debug/net8.0/BoatPhysicsCheck.dll
dotnet tests/EnvironmentCheck/bin/Debug/net8.0/EnvironmentCheck.dll
dotnet tests/GodotIntegrationCheck/bin/Debug/net8.0/GodotIntegrationCheck.dll
dotnet tools/SimulationHarness/bin/Release/net8.0/SimulationHarness.dll --calibration-review > /tmp/SailingSimulator-calibration.json
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot -- --performance-review
git diff --check
```

The generated report was also checked for seven exact replays, 1,800 steps,
31 trace observations, 600 final-window samples and the 58+80 kg mass split.

## First-playable evidence checklist

| Scope item | Evidence / status |
| --- | --- |
| ILCA 7 open practice and all eight controls | Implemented; boat/integration and actual scene checks pass. Model remains provisional. |
| Steady wind, 10-knot default, 0–30 knots and compass settings | Implemented; adapter/settings and scene checks pass. |
| Gentle default irregular waves affecting motion, calm/moderate options | Implemented; environment groups and shared rendering checks pass. Preset values remain estimates. |
| Repeatable conditions | Seed/environment tests and seven exact trajectory baselines pass. Saved F1 conditions alone do not replay a whole session. |
| Visual trim/luff/telltale/heel/wake cues and two camera views | Shared-state consistency and scene checks pass; previous Phase 6 fixture review remains applicable. Wider dynamic visual scenario review is still pending. |
| F1 numeric telemetry, live graphs and diagnostics; F2 controls | Scene checks pass, graph storage/drawing verified, desktop capture reviewed. |
| Steering, righting, trim, luffing, stopping and tack/jibe behavior | Existing physical-contract checks and new coasting baseline provide internal consistency evidence. Further reproducible engineering scenarios remain in Phase 8. |
| Compatible published speed, pointing and sail-force calibration | Deferred to Phase 11; not a first-playable prototype completion criterion. No verified numeric polar target/complete compatible conditions entered. See reference boundary in the protocol. |
| Stable desktop 60 FPS and 60 Hz simulation | Open: stalls and deadline misses remain, documented above. |
| Numerical claims distinguished from empirical validation | Recorded in protocol/results; parameters remain explicit estimates. Subjective sailing feel is unassessed and not a milestone requirement. |

## Next work

1. Investigate the gentle-wave speed range and the limits of the convergence
   screen below before using controlled runs for comparison.
2. Profile native drawing/layout, rendering and worker scheduling to explain
   the remaining first-open frames and sustained deadline misses. Preserve
   scheduling policy and repeat desktop measurements before closing the criterion.
3. Extend reproducible dynamic visual/handling reviews, then complete the
   prototype checklist only when engineering and performance evidence supports it.
4. Record a completion handoff with tested conditions, estimates and Phase 11
   data gaps. Compatible numeric target extraction, rig/crew/mass compatibility
   for comparisons, Chalmers sail-force comparisons and measurement-based tuning
   are Phase 11 work. Large thesis/dataset retrieval still needs user direction
   under the plan; none was undertaken in this pass. Do not wait for those
   targets to close Phase 8 once its engineering criteria actually pass.

## Controlled-course review — 2026-10-27

The initial `--course-review` added an offline test sailor, separate from the force model.
It starts from the existing north-wind, 10-knot conditions and fixed trim, runs
120 seconds at 60 Hz, and evaluates every step of the final 30 seconds. Heading
error wraps at 360 degrees. The controller requests tiller
`clamp(1.2 * heading_error_rad - 0.8 * yaw_rate, -0.7, 0.7)` and hiking
`clamp(-0.7 * heel_rad - 0.3 * roll_rate, -0.8, 0.8)`. It holds directional
keys toward those requests using the actual host ramp and release rules. These
are provisional feedback gains, not a sailor model or autopilot for the game.

The [compressed results](08-course-results.json.gz) retain all per-step held
keys and applied controls, one-second observations, conditions, runtime and
assembly hashes. Each run independently replays the recorded inputs and compares
complete states and diagnostics at every step. The command now uses the revised
controller described below:

```sh
dotnet run -c Release --project tools/SimulationHarness -- --course-review > course-results.json
```

The screen requires final-window speed range at most 0.1 m/s, speed half-window
mean change at most 0.05 m/s, heel and pitch mean changes at most 2 degrees,
heading error at most 3 degrees, absolute heel below 45 degrees, and changes in
mean net force and moment magnitudes at most 10 N and 10 Nm respectively. These
are engineering screening estimates, not measured accuracy limits; force and
moment mean changes alone cannot rule out oscillation or prove equilibrium.

| Case | Max heading error | Max heel | Screen |
| --- | ---: | ---: | --- |
| 45° calm | 6.56° | 2.29° | Fail: heading |
| 90° calm | 0.58° | 2.25° | Pass |
| 135° calm | 6.59° | 0.39° | Fail: heading |
| 90° gentle, seed 123 | 1.24° | 8.64° | Fail: speed range |

All four replays matched. These observations are model and controller behavior,
not real-boat calibration. Tests check repeatability, held-key control rates and
the force-change screen. Desktop timing, dynamic visual reviews and the other
engineering checks remain open; this work does not complete Phase 8.

### Bounded integral steering follow-up — 2026-10-27

The original controller settled with a heading error because a proportional
request needs a nonzero error to maintain a steady tiller offset. The revised
test sailor adds `0.08 * error_rad * dt` to an integral contribution clamped to
±0.4, then adds it to the previous heading/yaw feedback before the ±0.7 tiller
clamp. The sailor still issues ordinary held keys. An independent sailor is
also replayed alongside the simulation at every step. These gains are review
tool estimates; no boat physics changed.

In the [new results](08-course-integral-results.json.gz), the three calm cases
pass the unchanged screen. Maximum heading errors in their final windows are
0.016°, 0.008° and 0.025° respectively. The seeded gentle reach has a 0.122 m/s
speed range and still fails the unchanged 0.1 m/s threshold; wave-driven variation and the suitability
of applying a flat-water threshold to waves require further investigation.
The regression suite now includes the full 120-second upwind course and checks
the controller and state replay. Neither passing the provisional screen nor
adjusting the test sailor establishes empirical performance.

### Scheduling observation

The existing `FixedStepSchedule` anchors deadlines to the original epoch and
requires at least one full period between actual starts. Consequently a delayed
start shifts the earliest subsequent start by that delay; further delayed starts
accumulate. Completion still has the remainder of each period before the
deadline, so a start delay is not itself a missed deadline. Once accumulated
start delay and compute time exceed a period, later steps can remain late even
after the original stall ends. This is an implication of the currently specified
no-catch-up policy, demonstrated by the existing schedule regression; it is not
evidence identifying the source of the initial desktop stalls. The policy and
warnings are unchanged. Desktop profiling and any decision to revisit the
schedule remain open.

## Gentle-wave variability investigation — 2026-10-27

`--wave-review` repeats the controlled 90° reach at 10 knots with gentle
waves and seeds 1, 123 and 456. Each 120-second run retains the complete
held-key history and performs an independent exact replay. Four consecutive
30-second windows use every physics step and the unchanged flat-water screen.
The [results](08-wave-review-results.json.gz) include conditions, runtime and
assembly hashes, controls, one-second traces and window summaries. Reproduce
with `dotnet run -c Release --project tools/SimulationHarness -- --wave-review`.

The first window contains acceleration and fails for multiple reasons. In
each subsequent window all three seeds exceed the 0.1 m/s speed-range limit:
ranges are 0.109–0.168 m/s, while the half-window mean changes are at most
0.0076 m/s in magnitude. Estimated 30-second mean speeds from the retained
one-second samples are 2.237–2.244 m/s across those later windows. The seed
123 trace repeatedly rises and falls over a few seconds. This supports the
interpretation that the threshold flags ongoing wave-driven variation in
these runs, rather than a clear sustained speed drift. It does not prove
statistical stationarity, exclude slower drift, or justify a new acceptance
threshold for arbitrary wave conditions. Net-force and moment magnitude means
also do not prove force balance. The existing screen is unchanged and these
wave runs are not claimed converged or calibrated.

The added regression checks verify the complete-window boundaries and their
agreement with the final window for an exactly divisible run. The Release
solution build and calibration-condition checks pass. Dynamic visual and
handling evidence and the desktop performance defect remain open.

## Desktop start-delay attribution — 2026-10-27

The worker now records start lag relative to the original cadence, the sum
of overruns relative to each requested start, and the largest individual
overrun since startup. The performance review reports the change in that sum
over each view. An overrun can include prior step work, OS scheduling, or
waiting overhead; these measurements do not distinguish their causes. They
do not change scheduling or deadline warnings. The overload regression checks
that start-delay measurements remain visible.

One desktop run used the same Godot 4.7 Mono Debug build, Intel HD 530/Mesa
renderer, seed, warm-up and five-second windows described above. The solution
and Godot project built without warnings; all 14 integration groups passed,
including a 600-step isolated run with zero late steps. The desktop run had:

| View | FPS | Frame p95 / max, ms | Late steps | Start lag at end, ms | Window start overruns, ms |
| --- | ---: | --- | ---: | ---: | ---: |
| Hidden | 58.70 | 18.56 / 98.16 | 9 | 6.11 | 5.09 |
| Telemetry | 59.72 | 16.99 / 25.31 | 0 | 6.48 | 0.37 |
| Environment | 58.75 | 17.06 / 63.53 | 200 | 12.51 | 6.03 |
| Performance | 59.75 | 17.05 / 31.79 | 296 | 12.81 | 0.30 |

The largest start overrun observed since startup was 4.91 ms. In the final
view compute p95 was 5.145 ms, but the start lag had already reached 12.81 ms;
252 consecutive steps were late at the end. This demonstrates how retained
start delays can produce persistent deadline misses even when subsequent
computation is ordinarily short. It does not identify the initial cause of
the overruns or the long frame stalls, nor does it meet the performance gate.
The desktop review completed and captured its views; its raw console output
is recorded in [08-start-delay-results.log](08-start-delay-results.log).
Further profiling and dynamic visual/handling review remain necessary.
