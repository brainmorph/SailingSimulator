# Calibration baselines

Phase 8 begins with reproducible observations of the existing, uncalibrated
model. No speed polar, sail coefficient, mass allocation or wave parameter
has been tuned. [Results recorded on 2026-10-23](../phases/08-baseline-results.json)
include every setup, initial pose/velocity/rig, controls, wind/waves/seed,
mass assumptions, fixed step, runtime, build configuration and SHA-256 hashes
of the core, adapter and harness assemblies.

Run from the repository root, separately from desktop performance measurements:

```sh
dotnet build tools/SimulationHarness/SimulationHarness.csproj -c Release --verbosity minimal -m:1 /nodeReuse:false
dotnet tools/SimulationHarness/bin/Release/net8.0/SimulationHarness.dll --calibration-review > /tmp/SailingSimulator-calibration.json
```

The review runs offline, as fast as possible. Release only reduces its cost;
it does not change the Godot game's Debug configuration or establish wall-clock
60 Hz performance. The assembly hashes identify the exact binaries used for
the record. Compiler/runtime changes can affect hashes or floating-point results;
exact replay is checked within each run on the same runtime/build.

## Protocol and screening

Seven cases use wind from north, zero mean current and seed 123: no-wind
coasting from 3 m/s, head-to-wind from rest, flat-water starts at headings
45/90/135/180°, and the game's default 90° start with gentle waves. Their
initial controls are in the report; reach/gentle use the game's initial trim
and hiking. Other trim choices are provisional comparison setups. Waves are
aligned with wind. No-wind coasting includes boat-induced apparent wind.

Each case starts at the model's solved upright hydrostatic balance, with
zero angular velocity and boom angle 0.1 rad. All keys remain released for
1,800 steps (30 s); there is no heading or balance controller. The same held-key
ramp used by the host samples controls on each step. Two independent solvers
must agree exactly on every completed pose, rig, mass, input, component force
and sail section; a mismatch fails the command. The report retains a trace
at time zero, each second and the final step, plus full initial/final body and
rig states. Replaying waves additionally requires the reported seed, controls,
initial state and step sequence, not just selecting similar sea conditions.

The final 600 completed states (20.0167–30 s) supply the summary, rather than
only the decimated trace. A steady candidate must satisfy all of:

- Absolute change between the two half-window mean speeds ≤0.05 m/s.
- Maximum minus minimum horizontal speed ≤0.1 m/s.
- Heading error relative to the initial requested course ≤3°, using circular differences.
- Absolute heel <45° throughout that final window.

These are screening tolerances chosen for development, not measured ILCA
accuracy bounds. Qualification only permits further inspection: forces,
attitude and controls also need equilibrium/convergence review before a polar
comparison. A successful command means exact replay and a complete record;
individual cases may fail the steady screen. It does not mean calibrated physics.

## Initial results

Speeds below are simulated horizontal speed in m/s. Heading and heel columns
are final-window maxima in degrees. They are not target performance values.

| Case | Mean speed | Speed range | Half-window change | Heading error | Absolute heel | Steady candidate |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| Coast, no wind, calm | 0.271 | 0.223–0.333 | −0.054 | 0.17 | <0.01 | No |
| Head to wind, calm | 0.034 | 0.033–0.034 | −0.001 | 1.40 | <0.01 | Yes, nearly stopped |
| Initial 45°, calm | 2.512 | 2.338–2.609 | +0.135 | 51.05 | 1.92 | No |
| Initial 90°, calm | 1.913 | 1.902–1.924 | −0.011 | 10.61 | 1.30 | No |
| Initial 135°, calm | 1.083 | 0.886–1.300 | −0.207 | 55.14 | 3.29 | No |
| Initial 180°, calm | 1.727 | 1.406–2.037 | −0.324 | 81.83 | 3.12 | No |
| Default 90°, gentle | 1.968 | 1.929–2.051 | −0.037 | 10.41 | 6.57 | No |

All seven cases replay exactly. Coasting decreases from 3 to 0.223 m/s in
30 s, consistent with passive resistance; its continued deceleration correctly
fails the steady screen. The nearly stopped head-to-wind case qualifies.
The other cases turn away from the initial course or continue changing speed.
In particular, the nearly constant beam-reach speed is insufficient evidence
for a polar point: the calm boat finishes at heading 79.39°, not 90°.

The next comparison setup needs a documented, deterministic test sailor that
uses ordinary steering/hiking adjustments to hold course and balance, with
its control history retained and convergence checked over longer windows.
That controller belongs in review tooling, not the sailing force model.
Do not tune hull/sail coefficients to these uncontrolled speeds.

## Reference boundary

The [Phase 1 reference baseline](../phases/01-reference-baseline.md) still
defines source compatibility. No verified numeric VPP target has been entered.
The [2023 Chalmers/RISE paper's landing page](https://research.chalmers.se/publication/546299)
was rechecked: its apparent-wind cases and downwind scope remain partial sail
evidence. The browser rejected the full text's approximately 10.7 MB response;
it was not downloaded through a workaround. The
[2016 thesis landing page](https://research.chalmers.se/en/publication/245846)
confirms a candidate velocity/attitude study but supplies no verified numeric
table or compatible crew/rig assumptions in the page text.

No large thesis or coefficient dataset was retrieved. The previously identified
29.98 MB 2022 thesis remains deferred under the plan's source-retrieval rule.
Keep its downwind flat-water conditions separate from upwind/reaching cases
and from gentle waves. Equipped mass interpretation, study crew compatibility,
measured coefficients and wave ranges remain unresolved; all existing estimates
stay explicit. Subjective sailing feel remains unassessed.
