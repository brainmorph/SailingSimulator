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
- [Hunter Marine water-ballast-series manual, including H23.5 appendix](https://manualzz.com/doc/6336751/hunter-marine-h19--h23.5--h26-sailboats-owner-s-manual).
  Use the original drawings to verify geometry; indexed/OCR text can confuse
  the different boat appendices. The archive listing was discoverable during
  planning, but the original Hunter drawings have not yet been reviewed.

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
