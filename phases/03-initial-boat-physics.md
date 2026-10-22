# Phase 3: Initial Boat Physics

Completed 2026-10-22. See [the completion record](03-completion.md) for verification,
provisional-model limitations and the Phase 4 handoff.

## Objective

Implement the first useful ILCA 7 force model: a boat responds to steering and sail trim across six degrees of freedom in still water and steady wind.

## Depends on

Phases 1 and 2.

## Work

1. Add mass contributors for hull, rig, foils, fittings, and the separate 80 kg reference sailor. Document which masses and centers are measured versus provisional, and ensure equipped boat mass is not double-counted.
2. Implement gravity, hydrostatic restoring behavior, sampled hull buoyancy, and hull drag using the simplified parametric geometry and samples established from Phase 1.
3. Implement centerboard and rudder lift/drag as separate force sources. Make rudder/tiller input alter steering force continuously.
4. Implement reduced-order sail sections with trim-driven shape and twist. Use the same sail state for force calculations and the later renderer; expose section airflow/force information needed for luffing and telltales.
5. Apply mainsheet, traveler, vang, outhaul, cunningham, centerboard, and continuous hiking controls. Hiking shifts the sailor mass point and creates a righting moment.
6. Ensure forces and moments cover surge, sway, heave, roll, pitch, and yaw. Keep maneuvers emergent from controls and forces rather than scripted tack/jibe commands.
7. Add diagnostic outputs sufficient to inspect force contributions and resulting motion during development.

## Deliverables

- A still-water, steady-wind physics model using all first-scope controls.
- Explicit force/moment breakdown and sail-shape state in simulation output.
- Verification of force direction, mass balance, control response, six-degree motion, and numerical stability at the fixed step.

## Completion criteria

- Control changes produce gradual, explainable changes in force and boat response.
- Luffing reduces sail force, and sail shape/twist output corresponds to the state used by the aerodynamic model.
- Hull, sail, foil, gravity, and buoyancy contributions are independently inspectable.
- No maneuver is hard-coded as an action; tacks and jibes can develop from steering and trim.
- The model remains stable over representative inputs and has recorded limitations rather than undocumented tuning constants.

