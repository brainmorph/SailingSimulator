# Phase 8 progress

Started: 2026-10-23. **Phase 8 and the first-playable milestone remain open.**
This pass establishes reproducible baselines, reduces debug graph allocations
and improves performance attribution. It does not calibrate physics parameters
or establish empirical ILCA realism.

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
| Steering, righting, trim, luffing, stopping and tack/jibe behavior | Existing physical-contract checks and new coasting baseline provide internal consistency evidence. Further calibration scenarios remain. |
| Compatible published speed, pointing and sail-force calibration | Open: no verified numeric polar target/complete compatible conditions entered. See reference boundary in the protocol. |
| Stable desktop 60 FPS and 60 Hz simulation | Open: stalls and deadline misses remain, documented above. |
| Numerical claims distinguished from empirical validation | Recorded in protocol/results; parameters remain explicit estimates. Subjective sailing feel is unassessed and not a milestone requirement. |

## Next work

1. Extend review tooling with a documented deterministic course/balance
   controller, ordinary gradual inputs and a retained control history. Check
   longer-window speed, force and attitude convergence before comparing polars.
2. Verify compatible numeric targets and rig/crew/mass assumptions using the
   sources in the plan. Large thesis/dataset retrieval still needs user
   direction under the plan; none was undertaken in this pass.
3. Profile native drawing/layout, rendering and worker scheduling to explain
   the remaining first-open frames and sustained deadline misses. Preserve
   scheduling policy and repeat desktop measurements before closing the criterion.
4. Extend reproducible dynamic visual/handling reviews, then complete the
   checklist only when the source and performance evidence supports it.
