# Phase 1: Reference Data and Model Contracts

## Objective

Turn the research and architecture gaps in `PLAN.md` into an auditable baseline that physics and integration work can use without inventing calibration targets.

## Depends on

None. This is the first phase.

## Work

1. Review the cited ILCA handbook, class weight guidance, 2022 downwind VPP thesis, 2023 sail-force dataset, and 2016 Laser study. Extract relevant numeric data and source locations; record access dates where useful.
2. Build a compact reference table for boat dimensions, equipped boat mass interpretation, component mass estimates, sailor mass assumptions, sail-force coefficients, and available speed/pointing targets.
3. For each candidate VPP target, record wind speed, wind angle convention, trim/heel assumptions, water state, crew/rig assumptions, units, and compatibility limits. Do not combine incomparable conditions into one target.
4. Resolve what can be resolved from sources. Keep unsupported component centers of mass, hull sampling dimensions, wave ranges, and performance targets explicitly marked as estimates or open questions.
5. Define the minimum C#-facing contracts the later phases need: control values, environment configuration, simulation state, and units/coordinate conversion boundaries. Keep contracts small and document uncertain fields rather than designing speculative extension systems.
6. Add a calibration-condition format that can describe repeatable wind, wave seed/settings, controls, and recorded results.

## Deliverables

- A source-backed reference/calibration data document or structured data file.
- A short model-contract document describing state and control fields, units, coordinate conventions, ownership, and snapshot boundaries.
- Updates to `PLAN.md` where research resolves an open question or changes a working assumption.

## Completion criteria

- Every numeric value intended to tune the initial model has units and a source or a clearly stated rationale.
- Target data limitations and incompatibilities are visible; no missing polar values are silently filled in.
- The later implementation phases can define their public interfaces from the contracts without guessing coordinate or unit conventions.
- Unresolved matters are listed with a recommended next action and the phase that owns it.

