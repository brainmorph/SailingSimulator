# Phase 6: Visual Sailing Feedback and Cameras

## Objective

Make boat behavior legible through modest 3D presentation and visual sailing cues, without relying on normal-play numeric instruments.

## Depends on

Phase 5 and simulation sail/environment outputs from Phases 3–4.

## Work

1. Build readable low-fidelity representations of the ILCA hull, rig, sail, centerboard, rudder, sailor mass/body position, and water surface.
2. Render sail shape and twist from the simulation's aerodynamic sail state rather than a separate visual-only control calculation.
3. Show poor trim with visible luffing and reduced sail force; make sail response and boat/water response readable together.
4. Add telltales that respond to local airflow, including visible behavior as airflow separates or luffs.
5. Show heel, wake, and gentle irregular wave motion clearly enough to support steering, trim, tacking, jibing, stopping, and upwind/downwind practice.
6. Implement switchable onboard and third-person camera views. Mouse controls the camera and does not alter sailing controls.
7. Keep graphics lightweight and prioritize smooth rendering and clear cues over detail.

## Deliverables

- Two usable camera views and readable boat/environment presentation.
- Sail, luffing, telltale, heel, wake, and wave visuals driven by simulation state.
- A short visual review checklist covering key sailing situations and view usability.

## Completion criteria

- Sail controls visibly affect the same sail state that produces aerodynamic forces.
- Luffing is clear during under-trim and telltales react to airflow direction/state.
- Both camera views support observation while mouse camera movement leaves sailing state unchanged.
- Normal play communicates boat response visually without requiring debug gauges.

