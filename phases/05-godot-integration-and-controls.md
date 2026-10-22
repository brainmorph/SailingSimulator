# Phase 5: Godot Integration and Controls

## Objective

Connect the standalone simulation to a Godot C# application and make the boat controllable without giving Godot's physics engine ownership of boat motion.

## Depends on

Phases 2, 3, and 4.

## Work

1. Create the Godot scene and adapter that initialize the simulation, pass configuration and controls, and render from simulation snapshots.
2. Run simulation stepping on a dedicated C# thread at a hard 60 Hz wall-clock cadence, independent of render frequency.
3. Publish a thread-safe snapshot of current sailing control states from Godot; the simulation samples it each fixed step. Use hold-to-adjust controls for smooth response and fixed initial key bindings.
4. Publish completed simulation state safely for the main/render thread. Render the latest completed snapshot directly initially; keep the boundary suitable for future interpolation.
5. Convert between Godot and nautical simulation frames at this boundary only.
6. Implement initial controls for tiller, mainsheet, traveler, vang, outhaul, cunningham, centerboard, and continuous hiking/righting effort. Ensure camera controls remain mouse-driven.
7. Add orderly start, stop, and shutdown behavior for the simulation thread; avoid racing Godot node access from that thread.

## Deliverables

- Godot C# project and playable test scene connected to the library.
- Documented input/state thread boundaries and fixed default key map.
- Verification for thread-safe handoff, sustained stepping cadence, and render reads from valid snapshots.

## Completion criteria

- Godot renders the C# simulation state; Godot physics does not drive sailing dynamics.
- Control input remains safe under concurrent stepping and produces smooth, bounded adjustments.
- Simulation stepping is independent of rendering and targets a hard 60 Hz cadence.
- Scene lifecycle shuts the simulation thread down cleanly without cross-thread scene access.

