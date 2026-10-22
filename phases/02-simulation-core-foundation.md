# Phase 2: Simulation Core Foundation

## Objective

Create the standalone C# foundation for deterministic, fixed-step sailing simulation, with no Godot dependency.

## Depends on

Phase 1 model contracts.

## Work

1. Establish the repository's .NET/Godot project structure and a standalone simulation library that can build independently of Godot.
2. Implement core value types and state boundaries from the Phase 1 contracts: boat pose and velocities, angular state, control snapshot, environment configuration, and output snapshot.
3. Use SI units internally, nautical body axes (+X forward, +Y starboard, +Z down), and quaternions for orientation. Keep user-facing knots/degrees and Godot coordinate conversion outside the physics core.
4. Implement a fixed 1/60-second simulation step and RK4 integration for the chosen state representation. Keep force evaluation separable from integration so later subsystems can be added and inspected.
5. Provide deterministic initialization and a minimal stepping harness that accepts the same state/configuration twice and produces repeatable results.
6. Document numerical safeguards and define how invalid/non-finite state is surfaced; do not hide instability by changing the fixed cadence.

## Deliverables

- Buildable standalone simulation library and minimal harness.
- Documented public inputs, outputs, units, frames, and fixed-step API.
- Verification for deterministic stepping, integration behavior, and invalid-state reporting.

## Completion criteria

- The simulation project builds without referencing Godot assemblies.
- A fixed-step sequence is repeatable from identical initial state, controls, and configuration.
- Orientation and frame conversions have documented conventions and basic verification.
- The stepping API can accept force contributions from later phases without coupling physics to rendering or input devices.

