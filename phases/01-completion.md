# Phase 1 Completion Record

Completed: 2026-10-22

## Delivered

- [Reference and calibration baseline](01-reference-baseline.md): source-backed boat/sailor dimensions, research conditions, narrow numeric heel/trim trend checks, coefficient normalization, and visible limits for missing coefficients and polar data.
- [Minimum model contracts](01-model-contracts.md): SI units, nautical body axes, minimum control/configuration/state fields, thread ownership, snapshot boundary, and repeatable-condition format.
- [Calibration condition fixture](calibration-condition.example.json) plus a dependency-free .NET regression check at `tests/CalibrationConditionCheck`.
- `PLAN.md` now distinguishes the 2016 heel/trim study from the 2016 licentiate thesis and records the separate 2017 Laser VPP as a supplementary 80 kg crew reference.

## Regression test

Ran from the repository root:

```text
dotnet run --artifacts-path /tmp/SailingSimulator-phase1-artifacts --project tests/CalibrationConditionCheck/CalibrationConditionCheck.csproj
```

Result: `Phase 1 calibration condition contract passed.` The test uses only .NET built-in APIs and has no third-party package references.

## Remaining limitations and handoffs

- The exact 2023 sail-force coefficient matrix and full test grid remain untranscribed. Phase 3 may use only supported qualitative relationships until specific coefficient data is available; warn the user before any bulk retrieval.
- The 2022 thesis lists a 29.98 MB PDF. Its numeric VPP data and crew/rig assumptions remain unverified; warn the user before retrieving it.
- The 2016 article provides selected flat-water upwind heel/trim trend points, not a full polar, and transfer to the ILCA 7 model requires compatibility review.
- Equipped-boat mass interpretation, component masses/centers of mass, wave ranges, and compatible upwind/reaching speed targets remain open with owners listed in [the reference baseline](01-reference-baseline.md).
- Phase 2 can implement the minimal simulation boundary from [the model contracts](01-model-contracts.md). Phase 3 owns model-specific force assumptions and must keep unsupported values labeled as estimates.
