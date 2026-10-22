# Phase 7: Debug Tools and Performance

## Objective

Provide the planned F1 diagnostics and environment controls, and make fixed-rate performance issues visible instead of masking them.

## Depends on

Phase 5. Coordinate with Phase 6 for scene integration and visual overlay layout.

## Work

1. Implement F1 to toggle a detailed development overlay with simulation values, controls, force/moment breakdowns, angles, and live graphs from the same state used by the boat.
2. Add wind speed slider (0–30 knots, default 10 knots) and compass-labeled wind-from direction slider (0–360°, 0° north).
3. Add independent compass-labeled wave direction and calm/gentle/moderate strength controls, defaulting to gentle and aligned with wind. Expose repeatable seed/condition selection for comparisons.
4. Track fixed-step deadlines. Define late steps against the 60 Hz deadline; show a soft warning after 3 consecutive late steps and hard warning after 5. Do not add catch-up, dropped-time, or slow-motion behavior.
5. Expose rolling step-time percentiles, per-subsystem timings, managed allocations, and garbage collection activity.
6. Verify overlay values and controls map to the active simulation settings and state, and that the overlay can be hidden for normal visual-only practice.

## Deliverables

- F1 debug overlay with live graphs and detailed values.
- Wind and wave controls with correct ranges, labels, and defaults.
- Performance instrumentation and visible consecutive-late-step warnings.

## Completion criteria

- F1 consistently toggles diagnostics without interrupting simulation or controls.
- The wind/wave controls honor the plan's units, compass convention, presets, and defaults.
- Timing, allocation, and GC metrics are available, and deadline warnings trigger at the specified consecutive miss counts.
- No workaround conceals a sustained failure to meet 60 Hz.

