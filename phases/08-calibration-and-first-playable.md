# Phase 8: Calibration and First Playable

## Objective

Calibrate and review the complete open-practice experience against compatible published data and sailor feedback, then close the first playable milestone defined in `PLAN.md`.

## Depends on

Phases 1–7.

Read the [Phase 7 completion record](07-completion.md) before performance work.
Its isolated worker check passes, but the desktop run develops persistent
absolute-deadline misses after opening telemetry and shows first-open frame
stalls. Diagnose this defect before claiming the first-playable performance
criterion; do not hide it by changing the scheduling policy.

## Work

1. Establish repeatable baseline conditions and document wind, waves/seed, controls, and simulation build for every comparison.
2. Calibrate steady sailing first, then reaching and trim across wind angles, then tack/jibe transitions, following the sequence in `PLAN.md`.
3. Compare speed and pointing with extracted VPP targets only where wind, rig, crew, trim, and water assumptions are compatible. Record mismatches and uncertainty; do not tune toward incompatible values.
4. Use Chalmers sail-force data for the conditions it covers and state where extrapolation is being used.
5. Arrange review from an ILCA sailor because the project has no firsthand sailing experience. Capture specific observations about steering, trim, heel, luffing, stopping, and maneuver feel, then record resulting tuning changes.
6. Profile on a typical desktop. Diagnose missed 60 Hz steps and frame-rate problems at their source while preserving the cadence requirement.
7. Review the full initial scope and first playable milestone checklist in `PLAN.md`: ILCA 7 open practice, all listed controls, steady 10-knot default wind, gentle repeatable waves, F1 settings/diagnostics, visual cues, and both camera views.
8. Update documentation with calibrated ranges, known limitations, reproducible conditions, and any remaining data gaps.

## Deliverables

- Calibration results with condition metadata, data references, and rationale for chosen parameters.
- Sailor review notes and disposition of feedback.
- A first-playable checklist with evidence for each scope item and a short remaining-limitations list.

## Completion criteria

- The first playable milestone's agreed scope is implemented and reviewed end-to-end.
- Calibration claims are traceable to compatible measurements, documented assumptions, or explicit sailor feedback.
- Repeatable conditions reproduce the same comparison setup.
- Performance evidence supports stable 60 FPS presentation and 60 Hz simulation cadence on a representative desktop, or remaining defects are concretely documented and the milestone is not claimed complete.

