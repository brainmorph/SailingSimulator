# Phase 8: ILCA Engineering Readiness

Started 2026-10-23. See the [progress record](08-progress.md),
[baseline protocol](../docs/calibration.md), and [recorded results](08-baseline-results.json).
This phase and the first-playable milestone remain open.

Scope revised 2026-10-27: objective-data calibration has moved to
[Phase 11](11-calibration-and-realism-validation.md) for both boats. Existing
baseline results and progress remain evidence, not a claim of completion.

## Objective

Verify the ILCA open-practice experience through reproducible engineering,
handling and visual checks and desktop performance measurements. Deliver the
first playable prototype defined in `PLAN.md`, with explicitly provisional
physics. Compatible measured speed, pointing and sail-force targets are not
prerequisites for this phase or for starting Phase 9 after its completion.

Experienced-sailor feedback is not required. Subjective sailing feel remains
unassessed; internal consistency checks do not establish empirical realism.
Keep estimates and missing references documented for Phase 11.

## Depends on

Phases 1–7.

Read the [Phase 7 completion record](07-completion.md) before performance work.
Its isolated worker check passes, but the desktop run develops persistent
absolute-deadline misses after opening telemetry and shows first-open frame
stalls. Diagnose this defect before claiming the first-playable performance
criterion; do not hide it by changing the scheduling policy.

## Work

1. Establish repeatable baseline conditions and document wind, waves/seed, controls, and simulation build for every comparison.
2. Extend review tooling with a documented deterministic test sailor that uses ordinary gradual steering/balance inputs, retains its control history and checks longer-window convergence. Separate this tooling from the force model; uncontrolled free-sailing speeds are not calibration targets.
3. Use reproducible scenarios to verify steering direction, trim response, heel/righting trends, luffing with reduced drive, stopping and tack/jibe transitions. Record inputs, observable results and engineering acceptance criteria; verify visual cues and camera framing against the same simulation state. Quantitative agreement with a real boat belongs to Phase 11.
4. Profile on a typical desktop. Diagnose missed 60 Hz steps and frame-rate problems at their source while preserving the cadence requirement.
5. Review the full initial scope and first playable prototype checklist in `PLAN.md`: ILCA 7 open practice, all listed controls, steady 10-knot default wind, gentle repeatable waves, F1 settings/diagnostics, visual cues, and both camera views.
6. Update documentation with tested conditions, explicit estimates, known limitations and data gaps. Transfer published speed/pointing comparisons, Chalmers sail-force comparisons and measurement-based tuning to Phase 11 without requiring their completion here.

## Deliverables

- Repeatable engineering baselines with condition/build metadata, rationale for provisional parameters and a Phase 11 calibration handoff.
- Reproducible handling and visual checks with recorded inputs, acceptance criteria, results and validation limits.
- A first-playable prototype checklist with evidence for each engineering scope item and a short remaining-limitations list.

## Completion criteria

- The first playable prototype's agreed scope is implemented and verified end-to-end through reproducible handling, control, visual and regression checks.
- Unsupported parameters remain explicit estimates; internal consistency tests are identified separately from empirical validation. Missing compatible measurements do not prevent engineering completion. No calibrated-realism milestone is claimed.
- Repeatable conditions reproduce the same comparison setup.
- Performance evidence supports stable 60 FPS presentation and 60 Hz simulation cadence on a representative desktop, or remaining defects are concretely documented and the milestone is not claimed complete.
- A completion record accounts for every remaining item: engineering defects stay in Phase 8 until resolved; objective-data comparisons and tuning are explicitly handed to Phase 11. Changing this scope alone does not complete Phase 8.

