# Godot host (Phase 5)

The Godot C# project and playable scene belong here when Phase 5 connects the
physics model. The current solution builds without Godot or its NuGet SDK.
Use `src/SailingSimulator.Adapters` for SI/UI and NED/Godot conversions; Godot
nodes and input devices must never enter `SailingSimulator.Core`.

Reference the core and adapters from the future host project. Author the boat
mesh with local forward -Z, starboard +X, and up +Y. Run one simulation owner
thread, sample an immutable `SimulationInput` mailbox once per 60 Hz step, and
read `Simulation.Latest` on the rendering thread. The host must schedule real
deadlines and report misses as specified in `PLAN.md`; the Phase 2 offline
harness does not implement that scheduler.
