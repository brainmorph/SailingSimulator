# Phase 7 Completion Record

Completed: 2026-10-23

## Delivered

- F1 toggles a scrollable, three-tab development overlay without stopping
  sailing or changing held input. Repeat events are ignored; F2 remains the
  controls reference. Normal practice retains visual cues and a control hint.
- Environment sliders provide 0–30 knots (10 default) and compass-labeled
  0–360° from-bearings. Waves start gentle and aligned; unlocking direction
  retains the current bearing, and independent waves ignore subsequent wind
  direction edits. Zero wind retains the requested UI/aligned-wave bearing.
- Explicit signed-integer seed application, random seed selection, default
  settings and one session-local Save/Restore conditions slot. Ordinary edits
  retain the seed. Restoring conditions does not rewind boat state or time.
  Active readouts use completed physics settings, not pending widget values.
- Telemetry reads the same snapshot as rendering: six-degree state, speed,
  attitude, leeway, apparent wind, boom, all controls, world forces/body moments
  by source, and each aerodynamic sail strip's shape/luff values. Four bounded
  graphs show speed, heel, step duration and deadline lateness with actual
  simulation timestamps and duplicate suppression.
- Immutable worker diagnostics publish rolling 300-step compute p50/p95/p99
  and maximum, nested mass/RK4/inspection/hull/board/rudder/sail timings,
  worker allocation rate, process GC counts and approximate managed heap.
  Current full step/deadline timing includes metrics overhead. Render FPS and
  frame maximum use monotonic wall time, independent of simulation timing.
- F1 shows faults and None/Soft/Hard deadline status, consecutive and total
  misses. Thresholds remain three/five. The absolute 60 Hz schedule, fixed
  timestep and no-catch-up policy are unchanged.
- [Measurement boundaries and usage](../docs/debug-tools.md), updated launch,
  controls and architecture documentation, and `--performance-review` for live
  desktop measurements followed by four frozen tab captures. GPU readback
  happens only after measurements and joined worker shutdown.

No third-party dependencies, source downloads or physics tuning were added.

## Regression and scene verification

.NET SDK 8.0.128; Godot 4.7 stable Mono (`5b4e0cb0f`). Both projects build
with zero warnings/errors. Commands from the repository root:

```sh
dotnet build SailingSimulator.sln --verbosity minimal -m:1 /nodeReuse:false
dotnet build godot/SailingSimulator.Godot.csproj --verbosity minimal -m:1 /nodeReuse:false
dotnet tests/CalibrationConditionCheck/bin/Debug/net8.0/CalibrationConditionCheck.dll
dotnet tests/SimulationCoreCheck/bin/Debug/net8.0/SimulationCoreCheck.dll
dotnet tests/BoatPhysicsCheck/bin/Debug/net8.0/BoatPhysicsCheck.dll
dotnet tests/EnvironmentCheck/bin/Debug/net8.0/EnvironmentCheck.dll
dotnet tests/GodotIntegrationCheck/bin/Debug/net8.0/GodotIntegrationCheck.dll
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --headless --path godot --log-file /tmp/SailingSimulator-phase7-smoke.log -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot --log-file /tmp/SailingSimulator-phase7-render-smoke.log -- --smoke-test
/home/purple-eden/Desktop/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64 --path godot --log-file /tmp/SailingSimulator-phase7-performance.log -- --performance-review
git diff --check
```

The condition fixture, 14 core checks, 12 boat groups and eight environment
groups pass. All 14 integration groups pass. Four new groups cover:

- Nearest-rank percentile values, bounded-window eviction, allocation-rate
  arithmetic and immutable GC/heap observations.
- Profiled/unprofiled trajectory equality through control/environment edits,
  populated subsystem costs, nesting and reset.
- Allocating force-model observations published with completed worker states.
- Default/limit/compass/zero-wind/independent-wave settings, signed seed
  extremes, condition round trips and invalid inputs.

The isolated worker sample completed 600 steps in 9.9890 wall seconds,
with zero late steps. Last step was 4.898 ms; the last 300 compute samples
had p50/p95/p99/max 5.044/5.590/6.173/14.844 ms, approximately 134.1 KiB/s
worker allocations, and no process collections during that runner. An earlier
cadence attempt while builds and long physics regressions ran concurrently
failed its deadline assertion; this is retained as load sensitivity, not
treated as a successful cadence sample.

Actual scene smoke passes headless and with the Compatibility renderer.
It exercises F1 open/close/repeat handling, held keys, real sliders/options,
zero wind/alignment, independent direction, seed text application, save/restore,
active completed-state settings, telemetry, graph duplicate rejection,
performance publication, visible warning thresholds/reset and fault display.
Existing F2, focus, render stall, transforms, shared sail/wave geometry,
camera isolation and joined shutdown checks remain.

## Desktop results and unresolved performance defect

The local engine fell back from the unavailable NVIDIA driver to Intel HD
Graphics 530 (Mesa 26.0.3, OpenGL Compatibility). The final seed-123 live run
had no concurrent builds or heavy regressions. GPU captures were deferred
until after all timed samples. Each row covers five seconds after two seconds
of initial warm-up; tab-opening costs remain in the corresponding sample.

| F1 view | Mean FPS | Frame p95 / p99 / max (ms) | Compute p95 / max (ms) | Late steps in window | Consecutive at end |
| --- | ---: | --- | --- | ---: | ---: |
| Hidden | 59.95 | 16.87 / 16.99 / 17.13 | 4.725 / 5.152 | 0 | 0 |
| Telemetry | 58.95 | 16.94 / 26.42 / 64.52 | 6.171 / 12.764 | 236 | 236 |
| Environment | 59.95 | 16.86 / 16.98 / 17.06 | 4.797 / 5.661 | 300 | 536 |
| Performance | 59.55 | 16.91 / 17.30 / 62.25 | 5.358 / 13.008 | 300 | 836 |

This does **not** establish the sustained desktop 60 Hz requirement. Opening
telemetry coincided with frame stalls and persistent absolute-deadline misses,
even after typical compute costs returned well below 16.667 ms. Process GC
advanced to 6/2/1; this alone does not attribute the stalls to GC, rendering,
JIT, OS scheduling or a particular allocation source. The unchanged schedule
retains accumulated lateness, as required. Hard warnings remain visible.

An earlier version captured images between timed windows, contaminating the
frame/cadence samples. Those measurements are superseded by the table above;
the final review captures only after the measurement run. PNGs are at
`/tmp/SailingSimulator-debug-{hidden,telemetry,environment,performance}.png`.
Raster review confirmed readable telemetry, graphs, settings and warnings.
The frozen capture phase has its own frame/readback costs; use the printed
timed results for performance comparisons, not FPS shown during PNG capture.

## Phase 8 handoff

- Diagnose the desktop deadline defect and first-open frame stalls before
  claiming the first-playable performance milestone. Reproduce with the
  documented review command. Attribute main-thread allocations/GC, JIT and
  scheduling costs before changing code; do not reset deadlines, catch up,
  drop time or lower simulation rate to hide it.
- Physics coefficients, mass interpretations, wave estimates and missing
  verified numeric polar targets remain unchanged and uncalibrated. Obtain
  compatible references and sailor feedback as planned.
- Saved conditions are an environment setup, not a persistent full-session
  replay. Graphs show observed render snapshots, not every worker sample.
  Percentiles include warm-up and may be hardware/load dependent.

Next active phase: [Calibration and first playable](08-calibration-and-first-playable.md).
