# Phase 4: Wind and Wave Environment

## Objective

Add configurable uniform true wind and repeatable irregular waves that affect the boat's motion and handling.

## Depends on

Phases 2 and 3.

## Work

1. Implement uniform steady true wind with a 10-knot default, 0–30-knot range, and the plan's direction convention: compass direction wind comes from, where 0° is north.
2. Convert that user-facing direction into the documented simulation vector and calculate apparent wind from true wind and boat velocity.
3. Implement directional irregular waves with a seedable realization. Default wave direction follows wind while allowing independent direction.
4. Define documented calm, gentle, and moderate presets, with gentle as default. Record the chosen wave height/length ranges and rationale in the reference data.
5. Sample wave elevation/normal/velocity as needed at hull and foil points. Apply wave effects to buoyancy, drag, and practical steering/speed response; do not imply a full CFD model.
6. Ensure equal seeds and settings reproduce equal wave conditions and that independent wave direction does not mutate wind direction.

## Deliverables

- Wind and water configuration in the simulation API.
- Repeatable directional wave generator and documented presets.
- Verification for direction convention, apparent wind, seed repeatability, and wave effects on boat motion.

## Completion criteria

- Wind settings use the stated user-facing units and convert correctly to SI vectors.
- Gentle waves affect six-degree boat response and can be repeated for comparisons.
- Calm/gentle/moderate settings produce ordered, documented wave-strength changes.
- Wave and wind direction are independently configurable; wave alignment with wind is the default.

