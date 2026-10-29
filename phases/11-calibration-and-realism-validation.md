# Phase 11: Calibration and Realism Validation

Status: deferred until suitable objective comparison data is available.
Added 2026-10-27 at the end of the current roadmap. Implementation has not started.

Read [PLAN.md](../PLAN.md), the [phase index](README.md), the retained
[ILCA progress record](08-progress.md) and [baseline protocol](../docs/calibration.md).

## Objective

Calibrate the ILCA 7 and Hunter 23.5 against compatible objective references,
record comparison errors and uncertainty, and establish the calibrated
simulator milestone for the conditions actually supported by evidence.

Phases 8–10 deliver engineering-verified playable prototypes with provisional
physics. Their completion does not require this phase's data or calibration.
This phase may remain deferred without reopening their engineering milestones.
Subjective sailing feel remains unassessed and experienced-sailor feedback is
not a completion requirement.

## Depends on

- Completed Phases 8–10, their reproducible scenarios and documented estimates.
- Suitable objective references with enough configuration/condition metadata
  to support the proposed comparisons. Public sources or future supplied data
  may be used; no user-supplied dataset is assumed.

Reference preparation may happen earlier when useful. Missing data is a reason
to keep this phase deferred, not to block implementation in Phases 8–10.
Follow the plan's restrictions on large downloads and complex workarounds.

## Work

1. Review the ILCA source baseline and Phase 9 Hunter research record. Extract
   numeric targets only where wind definition, rig/sails, crew/loading, trim,
   ballast/board configuration and water conditions are compatible. Record
   units, source locations, exclusions and uncertainty. Distinguish measured
   results from VPP predictions and qualitative handling reports.
2. Resolve or bound the retained ILCA mass-budget and crew-compatibility
   questions and Hunter configuration assumptions before comparisons. Keep
   unknown coefficients, sail-interaction effects and wave presets explicit
   estimates where evidence is absent; do not invent targets or substitute
   another boat's polar.
3. Define comparison conditions and acceptance tolerances before tuning.
   Retain inputs, control histories, environment/seed, initial state, build
   metadata and repeatable trajectories for each boat. Verify course, force,
   speed and attitude convergence before treating a run as a steady point.
4. Calibrate steady sailing first, then reaching and trim across wind angles,
   then tack/jibe transitions, wherever compatible evidence exists. Use ILCA
   Chalmers sail-force data only within its documented coverage; label
   extrapolation. Keep downwind flat-water evidence separate from upwind,
   reaching and wave conditions. Apply the same compatibility rules to Hunter
   references; qualitative reports and hull-speed formulas are not polars.
5. Record before/after errors, parameter changes, rationale and uncertainty.
   Add appropriate regression checks for supported behavior. Keep internal
   consistency and empirical validation results distinct; exact replay alone
   does not establish realistic sailing performance.
6. Repeat affected handling, controls and dynamic visual reviews for both boats,
   including luffing, heel/righting, stopping, maneuvers and both cameras.
   Confirm rendering and force diagnostics still use the same completed state.
7. Repeat representative desktop performance measurements after physics changes:
   stable 60 FPS presentation and fixed 60 Hz wall-clock simulation cadence,
   including diagnostics and the full playable scenes. Preserve RK4, the
   existing scheduling policy and warnings; fix regressions at their source.
8. Update model documentation with calibrated conditions, supported ranges,
   remaining estimates and data gaps. Publish a completion record that limits
   realism claims to the evidence actually obtained.

## Deliverables and completion criteria

- An auditable reference/target register for both boats with conditions,
  uncertainty, compatibility decisions and explicit exclusions.
- Reproducible comparison results with acceptance tolerances, measured errors
  and rationale for parameter changes. Unsupported conditions remain labeled
  provisional; a research-gap record alone does not satisfy calibration.
- Both boats' agreed data-backed comparisons pass their documented criteria.
  If suitable evidence is still missing or comparisons fail, this phase and
  the calibrated simulator milestone remain open. Any later scope change
  must be explicit in `PLAN.md` rather than implied by completion wording.
- Affected engineering/regression and visual checks pass, and desktop evidence
  again supports stable 60 FPS and fixed 60 Hz cadence after calibration.
- Updated documentation separates the earlier playable prototypes from the
  calibrated simulator and states the limits of empirical validation.
