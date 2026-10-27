# Phase 9: Hunter 23.5 Physics, Verification and Performance

Status: started; reference/model contract and implementation remain incomplete.
See the [preliminary reference register](09-reference-progress.md) for the
initial configuration and performance-source research and unresolved gaps.

Read this with [PLAN.md](../PLAN.md) and the [phase index](README.md).
Phase 8 remains open. At the user's direction, Phase 9 work has started before
Phase 8 engineering completion; its outstanding timing defect remains open.

## Objective

Establish a provisional Hunter 23.5 physics model and verify its engineering
behavior before building the full player experience. Research available
references, implement hull and two-sail dynamics, check reproducible behavior
and measure real-time performance in a minimal Godot test scene. Physics
correctness and stable 60 FPS take priority over presentation detail.

Scope revised 2026-10-27: objective-data comparisons, measurement-based tuning
and calibrated-realism acceptance belong to
[Phase 11](11-calibration-and-realism-validation.md). Missing compatible
numerical targets do not block this phase or its handoff to Phase 10. Document
estimates and data gaps; numerical stability, model consistency and performance
are still required here.

[Phase 10](10-hunter-235-playable-integration.md) adds boat selection, complete
player controls, visual feedback and cameras using this engineering-verified
provisional model. Both phases require evidence for fixed 60 Hz simulation cadence and stable 60 FPS
presentation; performance work starts here rather than at the end of integration.

## Confirmed scope across Phases 9 and 10

| Area | Decision |
| --- | --- |
| Boat | Hunter 23.5 |
| Selection | Phase 10: choose before sailing and switch through F1 with a session restart |
| Jib handling | Independently ease/trim port and starboard sheets; player handles sheet changes |
| Ballast | Full and fixed during sailing |
| Centerboard | Adjustable during sailing |
| Sails | Full mainsail and jib; reefing and furling deferred |
| Skipper | Moves between port and starboard cockpit seats to affect balance |
| Performance references | Research trustworthy online sources; no user-supplied performance data is assumed |

## Phase boundary

Phase 9 owns reference research, shared simulation contracts, Hunter forces,
mass/rig dynamics, internal verification, deterministic scenarios and a minimal
real-time Godot test scene. Scripted inputs and a development launch option are sufficient
to exercise the Hunter here. The test scene must use the real simulation worker
and published states, with simple boat/sail geometry and development telemetry.

Phase 10 owns the player-facing boat picker, F1 boat switching, final keyboard
bindings/help, full telltales and sail feedback, recognizable Hunter geometry,
wake and both camera modes. Phase 9 publishes the geometry and per-sail state
needed for that work; it does not need the completed player interface.

Phase 11 owns objective-data calibration for both boats. Preserve any useful
references and comparison candidates from this phase for that handoff; finding
a compatible speed polar is not a Phase 9 completion requirement.

## Working assumptions to verify

- Reuse steady wind, wave settings/seeds and the existing performance diagnostics.
  Full visual sailing cues, F2 help and both cameras are integrated in Phase 10.
- Use the existing 80 kg reference skipper provisionally. Record Hunter seat
  positions, movement path and movement rate as estimates unless sourced.
- Use gradual cockpit movement through the existing body-position input
  concept, with Hunter-specific travel limits. Confirm bindings during control
  design; the skipper remains inside the cockpit rather than hiking outboard.
- Establish the actual main controls and jib lead positions from the selected
  Hunter reference configuration. Fixed jib leads are the initial proposal;
  additional rig adjustments are outside this draft's agreed scope.
- Engine propulsion, launching/trailering, ballast operations, and other sail
  inventories are future work. Full sails do not establish suitability at every
  wind speed available in F1; document the verified operating conditions.

## Work packages

### Immediate priority: Hunter physics and verification

On 2026-10-27 the user directed us to temporarily pause the focus on
performance issues. Prioritize the remaining rig behavior, boat diagnostics,
and reproducible engineering and regression checks. Resume investigation and
the full real-time performance scenarios later; the existing 60 Hz/60 FPS
completion gate and unresolved desktop stalls remain open.

#### Integrated Hunter milestone and progress

The earlier priority was an end-to-end development scene, using
the existing provisional model and documented estimates. Work packages below
may overlap; their numbering does not prescribe implementation order. The
milestone is a visible Hunter sailing in Godot with enough telemetry to inspect
its behavior, using the real simulation worker and completed snapshots.

1. Publish the boat geometry and completed main/jib state needed by the scene,
   including sheet positions, sail geometry, local airflow and force diagnostics.
   Clearly identify any provisional or unavailable diagnostics.
2. Connect a minimal Godot development scene with simple hull, sails, board and
   skipper markers. Scripted inputs and development telemetry are sufficient.
3. Run repeatable initial scenarios for steady sailing, a tack with manual sheet
   changes, and board and crew movement. Record inputs and conditions, and fix
   integration and numerical stability problems revealed by these runs.
4. Measure real-time simulation and presentation performance, including
   telemetry overhead. Investigate misses, including any relationship to the
   existing Phase 8 desktop timing defect, and record findings and headroom.
   Initial measurements are recorded below; further investigation is temporarily deferred.
5. Finish and verify the remaining rig behavior, including luffing, backwinding,
   both-tight and both-eased sheets, maneuvers and main/jib interaction, then
   complete the broader engineering verification below. The performance scenarios
   remain required later.

Use explicit estimates for unresolved dimensions rather than holding this
milestone for more reference searches. This sequence changes priority, not the
phase's engineering, regression, documentation or performance completion gates.

Progress: the shared control and held-input snapshots now carry independently
adjustable port and starboard jib sheets. They use normalized tension (0 eased,
1 trimmed), a provisional 0.25 per second ramp, and retain position on release.
The ILCA model ignores these inputs. No Hunter dynamics, keyboard bindings or
automatic sheet changes are implemented yet. The integration checks cover
independent ramps, bounds and snapshot immutability. These contracts are subject
to review against the eventual jib model.

The shared RK4 state also carries a provisional jib angle about the forestay
and its rate, with derivatives evaluated at each stage. Existing single-sail
models leave both at zero. A coupled analytic integration check and a non-finite
stage/atomic-publication check cover this infrastructure. This is a state
contract only: it does not implement sail geometry, constraints or jib forces,
and its suitability must be reviewed with the Hunter model contract.

The core now has an `IBoatDefinition` creation boundary with an ILCA
implementation, used by the Godot host. A regression check compares its
simulation against direct ILCA construction and checks independent instances.
Geometry, capabilities, per-sail output and a Hunter definition remain to be
designed against the reference/model contract; this boundary does not complete
work package 9.2.
The existing component mass and parallel-axis calculation is also available as
`MassAggregation.Combine`, retaining the ILCA calculation and checking invalid
input and a symmetric component example. A provisional Hunter mass model and
budget are recorded in [Hunter model work](../docs/hunter-235-model.md), with
tests for ballast, board and skipper accounting. A separately parameterized
sampled Hunter hull now has calm-water heave initialization and checks for
flotation, small-angle restoring and dissipative drag. Separate estimated
centerboard and rudder components reuse the provisional foil curves and have
deployment and steering checks. These components are connected through an
experimental Hunter definition; sail constraints and remaining geometry contracts
are incomplete.

An independent [two-sail force experiment](../docs/hunter-235-model.md) now
evaluates main and forestay-mounted jib sections, their airflow, summed forces
and compliant manual sheet limits. Tests cover zero wind, mirrored loads,
section sums and independent sheet constraints. Force composition and a short
deterministic replay are checked. Completed Hunter snapshots publish sectional
airflow, forces, sheet limits and section-edge geometry for each sail, plus
provisional hull, foil and skipper markers. Boat force diagnostics remain to be
published. Backwinding and maneuvers,
main/jib interaction, geometry and real-time stability remain to be verified;
the rig is not an engineering-complete model.

The initial `--hunter-dev` Godot host renders the provisional hull, sail
sections, foils and skipper from worker snapshots and displays sail forces,
sheet positions and timing. It uses fixed initial controls, calm water and a
simple placeholder scene. The headless `--hunter-smoke` checks the worker and
rendered geometry; scripted maneuver/crew/board scenarios, visual review and
desktop performance evidence remain outstanding.

The development host now also has a scripted worker exercise
(`--hunter-scenario`, or `--hunter-scenario-check` for an automated exit).
With 10-knot wind, calm water and seed 123, it holds the initial controls for
4 s, steers at +0.6 for 4 s, eases port and trims starboard jib sheets for
3 s, then raises the board/moves crew to starboard for 4 s and reverses both
for 4 s. A headless run reached steps 240, 480, 660, 900 and 1140 at the
stage boundaries; final sheet positions were 0.00/0.75, board 1.00 and crew
-1.00. Heading changed from 90.0° initially to 122.8° after steering and
125.6° at the end. The control movement check passed without a worker fault,
but this does not establish a completed tack, steady sailing, visual quality,
or performance. Input changes are published on render frames, so exact
boundaries can vary; a step-exact replay and verified tack remain outstanding.

The follow-up script corrects the steering direction and holds it from 4 to
30 s, with the manual port-ease/starboard-trim sheet change from 20 to 23 s.
Board/crew movements follow from 30 to 38 s. The same input schedule is shared
with an offline, step-exact replay that compares two simulations and checks
crossing the upwind heading upright. This establishes a heading crossing in
one provisional scenario, not a verified complete sail transition or a
calibrated maneuver. The real-time host samples the schedule on render frames;
its exact input boundaries can differ from the offline replay.
In one headless host run the heading was 21.1° at 20 s, 9.1° at 23 s and
-13.3° at 30 s; the final sheet, board and crew checks passed at 38 s without
a worker fault. This headless run is not desktop frame or cadence evidence.

The latest script continues steering until 44 s, then releases the tiller for
10 s before the board/crew movements at 54–62 s. Offline checks require both
sails to cross after the upwind heading, then have average sectional luffing
below 0.1 and boat speed above 1 m/s at 54 s. These are scenario-specific
engineering checks, not empirical validation or proof of steady sailing.
The exact-step run observed boom/jib angles of about -0.74/-0.57 rad and
speed 1.46 m/s at 54 s; the headless worker run reached -87.0° heading at
54 s and completed the control sequence at 62 s without a worker fault.

A steady-reach replay now holds the same calm-water, 10-knot, seed-123
environment and initial trim, with a fixed -0.1 tiller, for 60 s. Two offline
simulations reproduce the same boat, rig and sail total at every step. During
40–60 s, speed was 1.933–1.935 m/s, heading 102.75–103.29°, maximum heel
1.28°, and average section luffing for either sail stayed below 0.001. The
scenario checks bounded changes, upright attitude and filled sails; these are
internal engineering criteria, not calibrated performance. With centered
tiller the provisional boat instead turns substantially, so the fixed helm is
part of the stated scenario. A worker-host version uses render-thread feedback
to maintain the helm via held inputs and is not step-exact. A headless host run
also passed, observing 1.933–1.935 m/s and 102.75–103.29° over 40–60 s, with
maximum heel 1.28° and average luff below 0.001. Desktop visual and performance
evidence remains outstanding.

An early desktop Hunter review on 2026-10-27 used the Debug Godot 4.7 .NET
build, Linux 7.0, Intel i7-6700HQ and Mesa Intel HD Graphics 530 (OpenGL 4.6
compatibility). It used the fixed initial controls, 10-knot wind, calm water,
seed 123, two seconds of warm-up and ten seconds each with telemetry hidden
and visible. The worker completed 600 and 601 steps respectively, with no
deadline misses in either window. Hidden/visible frame p50/p95/p99/max were
16.68/17.49/18.75/91.23 ms and 16.69/17.38/19.27/27.03 ms; average FPS was
59.34 and 59.96. The rolling 300-step compute p95 was 8.89/5.87 ms and
worker allocation about 940 KiB/s; main-thread allocation was 108/135 KiB/s.
These consecutive short windows do not isolate telemetry overhead or prove
headroom; the hidden-window 91 ms stall leaves smooth presentation unproven.
The captured scene showed the hull and two sails, but their small size and
overlap made the skipper and underwater markers difficult to inspect. Further
visual review, stall investigation and the full performance scenarios remain
open. Reproduce with `--hunter-performance-review`; details and metric limits
are in the Godot README.

A follow-up on the same desktop and build added frame-aligned process GC/JIT
correlation and managed scene-update timings. Hidden/visible ten-second
windows recorded 596/596 frames, maxima of 86.15/73.65 ms and zero worker
deadline misses. Neither longest frame overlapped a measured GC pause or JIT
advance. The largest managed render update in either window was 4.96 ms;
visible telemetry updates averaged 0.040 ms and peaked at 0.136 ms. The long
frames therefore are not explained by these measured managed updates or by
the sampled GC/JIT counters. Native drawing, GPU work and OS/compositor
scheduling are outside these measurements; this does not establish which of
them caused the stalls. Similar unexplained desktop stalls were observed in
Phase 8. The Hunter gate remains open pending further investigation and longer,
representative scenario measurements.

A further attribution run measured the Hunter callback and the gap between
its sampling point and the next `_Process` entry. With the normal desktop
settings, the hidden/visible maximum frames were 76.19/83.04 ms, of which
75.91/82.67 ms occurred in the gap outside the sampled callback. Maximum
sampled callback time was 0.78/1.51 ms. There were no worker deadline misses
in those windows. The gap includes native frame work, presentation waits,
OS scheduling and a small unsampled callback tail; it does not identify a
specific cause. A diagnostic comparison with `--disable-vsync --max-fps 60`
had frame maxima of 16.81/16.86 ms in two ten-second windows. In that run,
however, start lag was already 52.57 ms at the end of the first window and
53.75 ms at the end of the second, and all 601 steps in each window were
late. This is consistent with the retained start-delay behavior documented in
Phase 8, but does not identify its initial cause. A single changed-settings
comparison suggests presentation synchronization warrants investigation; it
does not prove causation or satisfy either performance gate. The diagnostic
flags do not change the project's normal rendering or scheduling settings.

On a repeated capped/no-VSync diagnostic, the warm-up ended with 0.82 ms
start lag and no late steps. Both subsequent windows also had no late steps;
lag reached 5.94 ms by the end. The earlier 53 ms lag was therefore not
reproduced and its cause is still unknown. A subsequent normal-settings run
lost focus, emitted ILCA input-map errors from the parent scene, and showed
strongly altered frame rates. Its frame figures are excluded from the focused
comparison. The Hunter host now prevents the parent ILCA scene from handling
Hunter input and focus events, and the performance review flags unfocused
windows and records warm-up timing. A further run had no input-map errors but
both windows were flagged unfocused, so it too is not focused-window evidence.
The earlier measurements did not record focus state and should be treated as
exploratory rather than verified focused-window results.

The normal-settings review was repeated with focus tracking: both ten-second
windows remained focused. Warm-up ended with 0.95 ms start lag and no late
steps; each window completed 600 steps with zero late steps, ending at 2.30
and 4.52 ms lag. Hidden/visible frame p95/p99/max were 17.32/18.91/81.44 ms
and 17.13/17.90/89.83 ms. The longest frames included 81.26/89.43 ms between
the sampled callbacks, with no measured GC pause or JIT advance over either
interval. This focused run confirms that the long-frame issue is not solely
an artifact of lost focus; the source and performance gate remain open.

An isolated rig check now verifies that both tight jib sheets restore openings
on either side, while both eased sheets exert no constraint at ±0.5 rad.
It also checks that reversing lateral airflow reverses the load on a centered
jib without changing a held sheet's limits, and that chord-aligned airflow
luffs both centered sails. These are static model checks, not a demonstrated
backwinded maneuver; dynamic old-sheet retention, both-direction maneuvers and
main/jib interaction remain to be verified.

A 54-second offline comparison uses the scripted 10-knot, calm-water steering
schedule with the starboard sheet initially at 0.65. The jib is at +0.478 rad
when the sheet change begins, on the starboard side. The port sheet is trimmed
in both runs; the starboard sheet is eased in one and held in the other. Both
jibs reach approximately -0.603 rad at 54 s, and the held run's average jib
luffing is 0.950. The prior comparison called the port sheet the old sheet
even though the initial jib was on the starboard side; this corrected setup
supersedes it. The current one-sided opening limit does not hold the clew on
the old side. The comparison checks the input schedule and records behavior;
it is not a passing backwind acceptance test. A suitable sheet-geometry
constraint and dynamic backwind verification remain open. An initial
geometry-based constraint experiment changed the steady reach enough to fail
its existing settling check, so it was not retained; the interaction with
trim, rig dynamics and the existing scenarios needs further investigation.
An isolated [sheet-geometry experiment](../docs/hunter-235-model.md#isolated-sheet-geometry-experiment)
now checks clew-to-lead tension, slack, symmetry and torque against spring
energy. A fixed-hull prescribed-wind exercise also shows the held sheet
retaining the jib on its starting side in both mirrored cases, while easing it
allows crossing; a stronger-wind case crosses slightly even with the sheet
held. It is not connected to the simulation; force accounting and full-boat
behavior still need verification before it can replace the current constraint.

### 9.1 Reference boat and model contract

1. Identify a consistent Hunter 23.5 configuration from manufacturer sailplan,
   rigging, deck-layout and centerboard drawings. Avoid mixing Hunter 23,
   Hunter 240, or different sail inventories into this boat's baseline.
2. Record dimensions, sail outlines and attachment points, board geometry,
   ballast location, mass budget, centers of mass and inertia assumptions.
   Resolve whether quoted displacement includes ballast, crew and equipment;
   count each mass once. Keep tank ballast distinct from centerboard mass.
3. Verify mainsail and actual jib area separately. Foretriangle dimensions
   alone do not establish the installed jib's area or outline.
4. Separate sourced values, calculated values and estimates. Record units,
   source page/drawing, configuration and uncertainty.
5. Conduct and record an initial Hunter 23.5 performance-reference search. Seek measured
   boat speeds versus wind speed/angle, pointing or tacking angles, heel and
   trim response, and stability data. Search manufacturer/designer archives,
   original instrumented sea trials, sailmaker/VPP publications and rating
   authority records. A polar is a table or plot of speed by wind strength and
   angle; locate an attributable original rather than an unexplained repost.
6. Assess each candidate's evidence and compatibility. Distinguish actual
   measurements, VPP/rating predictions, qualitative firsthand sailing reports
   and estimates. Record true versus apparent wind, speed through water versus
   over ground, current/sea state, crew/loading, ballast/board position, sail
   inventory, reefing, units and source location wherever available. Unknown
   conditions limit a source's use; they must not become silent assumptions.
7. Produce a source register, search log and any usable target table, including
   uncertainty and exclusions. Give measured data priority where available;
   label any VPP targets as predictions. Use qualitative reports as handling
   clues, and handicap ratings only as broad cross-checks, not speed polars.
   Document where no trustworthy numerical target was located and keep those
   model parameters provisional. Do not substitute another Hunter model's
   polar, infer a polar from hull speed, or invent missing measurements.

Deliver a Hunter reference record, initial performance-research findings and
explicit model assumptions before implementing forces. Record sources checked,
compatibility decisions and remaining gaps; unavailable numeric targets do not
require an open-ended search or prevent provisional implementation/completion.
Phase 11 owns further target extraction, comparisons and measurement-based tuning.
No user-supplied data is required. Follow the plan's existing restrictions on
bulk downloads.

### 9.2 Shared support for boat models and multiple sails

1. Introduce a small boat definition/factory boundary for geometry, mass model,
   initial state, control capabilities and presentation dimensions.
2. Extend control snapshots for independent jib sheets and boat-specific crew
   movement. Preserve held-key ramps and immutable thread-safe publication.
3. Extend rig dynamics and RK4 state handling for the chosen jib representation.
   The current `RigState`/derivative carry only boom angle and rate; any new
   dynamic state must participate consistently in every integration stage.
4. Publish completed-state shape, local airflow and force/moment diagnostics
   per sail, with stable main/jib identities and an unambiguous summed total.
   Remove single-sail assumptions from consumers and deterministic comparisons.
5. Publish boat geometry, sail attachment points, stern and cockpit positions
   without ILCA-only constants. Use these contracts in the minimal test scene;
   full wake and camera consumers are integrated in Phase 10.

Keep the C# core Godot-independent, use SI units and the existing coordinate
conventions, and retain the 60 Hz RK4 and simulation-thread ownership contracts.

### 9.3 Hunter hull, mass and underwater forces

1. Build a Hunter-specific sampled hull with plausible floating equilibrium,
   displacement, trim and restoring response. Reuse algorithms where useful;
   establish hull resistance separately from the ILCA coefficients.
2. Represent full tank ballast in the mass budget and inertia, with estimated
   geometry explicitly recorded. Raising the board does not remove ballast.
3. Model centerboard deployment, area, position and lateral resistance using
   Hunter geometry, plus a separate rudder force source with tiller steering.
4. Move skipper mass along a bounded path between cockpit seats. Publish that
   same position for presentation and the later onboard camera. Preserve the
   existing documented mass-repositioning invariants and approximation limits.

### 9.4 Mainsail and manual jib physics

1. Parameterize the Hunter mainsail and model the jib attached to a sloping
   forestay, with distinct tack, head, clew and sheet leads. A jib has no boom;
   do not apply the ILCA boom geometry directly to it.
2. Calculate sectional apparent wind and lift/drag for both sails, including
   their force locations and resulting heel/yaw moments.
3. Define two sheet constraints with independent trim/ease inputs. Model slack
   sheets, luffing, filling on either side, and backwinding when the old sheet
   remains tight. Define both-tight and both-eased behavior explicitly.
4. Let sail crossing and maneuvers emerge from steering, airflow and sheet
   constraints. Heading changes must not automatically release an old sheet.
5. Establish a bounded reduced-order treatment of main/jib airflow interaction
   and document its assumptions. Compare with independent-sail behavior and
   distinguish estimated interaction effects from measured aerodynamic data.
6. Publish geometry and local airflow from the same completed sail states used
   to inspect forces. Verify simple test-scene sail geometry against this state;
   full telltales follow in Phase 10. Keep numerical stability checks separate
   from claims of aerodynamic or full-boat calibration.

### 9.5 Physics verification and calibration handoff

1. Extend dependency-free checks for sheet input independence, snapshot ownership,
   complete rig-state integration, mass-repositioning invariants and deterministic
   replay. Add Hunter identification and both-sail comparison to the harness.
2. Check the Hunter mass budget, static flotation, restoring response, seat
   movement and board effects. Distinguish model consistency from measured
   stability validation; use compatible references when available.
3. Exercise zero wind, mirrored port/starboard cases, full/slack/both-tight
   sheets, backwinding, both-direction tacks/jibes and poor trim. Record actual
   control sequences and verify luffing/drive trends under stated conditions.
4. Verify per-sail published geometry/airflow and force diagnostics agree, and
   the combined force/moment total includes each contribution exactly once.
5. Run existing ILCA checks after shared changes and compare repeatable baseline
   behavior. Make unexplained regressions part of this phase's remaining work.
6. Retain reproducible scenarios, any compatible reference candidates and
   parameter assumptions for Phase 11. Document internal engineering acceptance
   criteria separately from future empirical comparison tolerances. Missing
   numeric targets do not fail this phase; estimated parameters remain explicit.
   Do not claim calibrated Hunter performance from internal tests, qualitative
   reviews or a generic hull-speed formula. Objective-data comparisons and
   tuning are Phase 11 work, not prerequisites for Phase 10.

### 9.6 Minimal Godot scene and real-time performance gate

1. Connect the Hunter to the existing dedicated-thread runner and render simple
   hull, main, jib, board and skipper markers from completed snapshots. Include
   development telemetry for per-sail forces, sheet states and timings. A full
   boat picker, production cameras or polished visuals are not prerequisites.
2. Run the deterministic scenarios in this real-time host: steady sailing,
   maneuvers, crew/board movement and calm/gentle/moderate waves, with telemetry
   both hidden and visible. Include poor trim and the existing wind-range stress
   cases; record separately where full-sail realism has not been validated.
3. Measure on a documented representative desktop using the existing performance
   protocol. Record frame-time distributions and sustained stalls, worker step
   timing/deadline misses, allocations and GC, hardware, build, conditions and
   run duration. An offline harness throughput result is not a cadence or FPS
   measurement. A 60 FPS average does not establish smooth frame delivery.
4. Require fixed 60 Hz wall-clock simulation cadence (about 16.67 ms per step)
   and stable 60 FPS presentation in the test scene, with capacity for the
   remaining Phase 10 rendering/UI work. Record measured headroom rather than
   assuming that passing the minimal scene proves final-game performance.
5. Profile and fix misses at their source. Preserve the existing scheduling
   policy and physics requirements; do not conceal failures with slow motion,
   dropped steps, simplified validation cases or silent fidelity reductions.

## Deliverables and completion criteria

- Hunter reference/model record with an auditable mass budget and sail geometry.
- Documented initial online performance-reference search with source-quality
  assessments, any usable targets and their conditions, rejected/incompatible
  references and explicit remaining gaps. Missing numeric targets do not block
  completion; documenting a gap does not establish measured performance.
- Hunter hull, ballast, adjustable board and movable cockpit skipper implemented.
- Main and jib respond to independent manual sheets, with observable luffing,
  backwinding and maneuver behavior supported by reproducible checks.
- Reproducible physics checks meet documented engineering criteria, including
  numerical stability, mass/force consistency and handling. Unsupported realism
  stays explicitly provisional. Objective-data comparisons and tuning are
  explicitly handed to Phase 11; engineering defects must be resolved here.
- Minimal Godot test scene and measured 60 Hz simulation/60 FPS rendering
  evidence, including telemetry overhead and remaining headroom. Unresolved
  performance defects keep this phase open.
- Existing ILCA regressions pass, and reproducible Hunter scenarios and model
  documentation provide a concrete handoff for Phase 10.
- A concise completion record identifies engineering-tested conditions,
  limitations, provisional parameters, Phase 11 data gaps and the exact
  build/scenarios used for the Phase 10 handoff.

## Reference starting points

- [Hunter 23.5 manufacturer-manual archive listing](https://hunter.sailboatowners.com/dl/downloads.php?mid=8&task=model).
- [Hunter Marine water-ballast-series manual scan](https://pacificsailors.com/www/wp-content/uploads/hunter/14-25/Hunter_235_Owners_Manual.pdf),
  including the H23.5 appendix. The original sail plan, rigging, deck layout
  and centerboard-control drawings have been inspected; see the reference
  register for page numbers, values and remaining limitations. Indexed/OCR
  text can confuse the different boat appendices.

These are research leads, not an extracted numerical calibration baseline.

### Initial performance-source screening

- [Practical Sailor's original Hunter 23.5 sea trial](https://www.practical-sailor.com/sailboat-reviews/hunter-23-5/)
  describes its own sailing tests, including light-air response and stronger-wind
  handling with a reefed main. It provides qualitative evidence, not a numerical
  speed/angle polar. Reefed-main observations do not directly calibrate this
  phase's full-sail configuration.
- [ORC's certificate documentation](https://orc.org/organization/monohulls/orc-club-certificate)
  explains rated speed tables produced by its VPP.
  [ORC Sailor Services](https://orc.org/sailors/sailor-services) is a research
  lead for locating a compatible certificate. No Hunter 23.5 certificate or
  associated polar has been verified during this initial search; generic ORC
  documentation is not Hunter performance data.

The initial search has not established a trustworthy numerical Hunter 23.5
polar. Record the initial search and configuration review in 9.1; this screening
does not complete that work package. Further target extraction and calibration
belong to Phase 11; failure to find a compatible polar does not block Phase 9.
