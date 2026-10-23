# Implementation Phases

This folder turns [PLAN.md](../PLAN.md) into independently actionable work for future agents. Work through the phases in order unless a phase explicitly allows parallel preparation. Each phase file is intended to be a handoff: read it together with `PLAN.md`, implement its scope, record evidence and decisions, then report completion and blockers.

## Phase sequence

| Phase | File | Outcome | Depends on |
| --- | --- | --- | --- |
| 1 | ~~[Reference data and model contracts](01-reference-data-and-model-contracts.md)~~ | Auditable calibration data and concrete simulation contracts | None |
| 2 | ~~[Simulation core foundation](02-simulation-core-foundation.md)~~ | Standalone deterministic C# simulation library and stepping foundation | 1 |
| 3 | ~~[Initial boat physics](03-initial-boat-physics.md)~~ | A controllable six-degree-of-freedom ILCA 7 model in steady conditions | 1, 2 |
| 4 | ~~[Wind and wave environment](04-wind-and-wave-environment.md)~~ | Configurable, repeatable wind and handling-affecting waves | 2, 3 |
| 5 | ~~[Godot integration and controls](05-godot-integration-and-controls.md)~~ | Playable Godot scene with threaded simulation and sailing controls | 2, 3, 4 |
| 6 | ~~[Visual sailing feedback and cameras](06-visual-sailing-feedback-and-cameras.md)~~ | Readable boat, sail, water, telltales, and camera views | 5 |
| 7 | ~~[Debug tools and performance](07-debug-tools-and-performance.md)~~ | F1 environment controls, telemetry, and 60 Hz diagnostics | 5; may overlap 6 after scene integration |
| 8 | [Calibration and first playable](08-calibration-and-first-playable.md) | Calibrated open-practice milestone meeting `PLAN.md` scope | 1–7 |

## Handoff rules

Phase 7 evidence and remaining desktop timing defect: [completion record](07-completion.md).
The next active phase is [Calibration and first playable](08-calibration-and-first-playable.md).
Phase 8 has started; see its [progress and milestone evidence](08-progress.md)
and [recorded free-sailing baselines](08-baseline-results.json). It remains open.

- Treat `PLAN.md` as the source of truth. Preserve its confirmed decisions; flag conflicts instead of silently changing them.
- Keep each change within the active phase. Update `PLAN.md` when a confirmed decision, assumption, source, or open question changes.
- Strike through a phase's name in the phase list only after its completion criteria are met, including appropriate regression tests and a concise completion record.
- Before completing each phase, add appropriate regression tests for the behavior, contracts, or artifacts introduced by that phase. Later phases should keep those tests and extend them as needed. Choose test scope based on the phase; do not add third-party libraries.
- Prefer measured values and cite their source, units, conditions, and assumptions. Mark estimates as estimates.
- Keep the simulation library independent of Godot, use SI units and the documented nautical body axes internally, and convert at the adapter boundary.
- For each phase, leave a concise record of implementation, regression tests added and verification performed, remaining limitations, and any follow-up work. Do not claim a criterion is met without evidence.
- Phase completion criteria describe work to implement and verify; they do not authorize skipping verification appropriate to the change.

## Commit messages

Use Google-style commit messages: write a concise subject of at most 50 characters in imperative mood, capitalize it, and omit the final period. Separate an optional body with a blank line and wrap it at about 72 characters. Explain context or rationale in the body when the subject is not enough.

