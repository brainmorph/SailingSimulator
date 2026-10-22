# Phase 1: Minimum Model Contracts

These language-neutral contracts give Phase 2 and later phases a concrete boundary without committing to speculative extension systems. Implement them as plain C# types using the .NET and Godot APIs already selected by the project; do not add third-party libraries.

## Coordinate and unit boundaries

- The simulation core uses SI units: metres, seconds, kilograms, newtons, newton-metres, and radians.
- Simulation body axes are right-handed: +X forward, +Y starboard, +Z down.
- World/reference axes and Godot axes are explicit at the adapter boundary. Do not pass an unlabeled `Vector3` across that boundary.
- Wind direction settings are compass directions **from which** wind comes: 0° is from north, increasing clockwise. Convert this setting once at the environment adapter into a documented SI velocity vector in the simulation's world frame.
- Internal angular values are radians. Convert to degrees and knots only for user-facing controls and displays.
- Orientation is a quaternion. Derive heel, pitch, and heading for display rather than using them as the internal orientation state.

## Input and configuration

### `SailingControls`

One immutable snapshot sampled at the start of each fixed simulation step:

| Field | Contract |
| --- | --- |
| `Tiller` | Normalized [-1, 1]; sign-to-rudder mapping documented by the Godot adapter. |
| `Mainsheet`, `Traveler`, `Vang`, `Outhaul`, `Cunningham` | Normalized [0, 1], where 0 is fully eased/released and 1 is fully trimmed/tightened. |
| `Centerboard` | Normalized [0, 1], where 0 is raised and 1 is fully lowered. |
| `Hiking` | Normalized [-1, 1], where sign selects side and magnitude is continuous hiking effort/position. Final interpretation is a physics-model decision; keep mapping explicit. |

The Godot side owns physical keyboard/mouse input. The C# simulation owns sailing dynamics and consumes only this thread-safe current-state snapshot. Input changes must not mutate a step already in progress.

### `EnvironmentConfig`

| Field | Contract |
| --- | --- |
| `TrueWindSpeedMps` | Scalar speed in m/s, initially constrained by the planned 0–30 knot UI range (converted at the boundary); default corresponds to 10 knots. |
| `WindFromDegrees` | Compass direction from 0° through 360°; 0° means from north. Boundary-only representation. |
| `WavePreset` | `Calm`, `Gentle`, or `Moderate`; `Gentle` is the initial default. Physical ranges remain unresolved and must be documented when selected. |
| `WaveDirectionMode` | `AlignedWithWind` or `Independent`. |
| `WaveFromDegrees` | Optional independent compass direction in degrees; used when direction mode is independent. |
| `WaveSeed` | Integer seed for repeatable wave realizations. |
| `WaterCurrent` | Omitted in the initial contract; initial water is still. Add only when current is brought into scope. |

The F1 debug UI will eventually edit environment settings, but that UI is outside Phase 1. The core receives a complete configuration snapshot, not UI controls.

## Simulation state and ownership

### `SimulationState`

An immutable completed-step snapshot published for rendering and diagnostics:

- simulation time in seconds and completed step index;
- position in metres in the documented world/reference frame;
- orientation quaternion mapping body axes into that world/reference frame;
- linear velocity in m/s and angular velocity in rad/s, with frame stated in the type/member name;
- current `SailingControls` and environment configuration used for that step;
- optional model outputs when available (forces, moments, apparent wind, derived heel/pitch/heading, sail shape); do not invent values for outputs not yet modeled.

The C# simulation owns and advances state. Godot owns rendering and publishes the input snapshot. At first, rendering reads the latest completed state directly. Snapshot publication must be thread-safe; interpolation is deferred unless a later phase finds it necessary.

## Calibration condition record

Use a small JSON-compatible record so a condition can be repeated and its outcome compared. The dependency-free fixture at [calibration-condition.example.json](calibration-condition.example.json) intentionally contains no unsupported physical wave numbers or performance targets. Its contract is covered by the Phase 1 regression check in `tests/CalibrationConditionCheck`; run it with `dotnet run --project tests/CalibrationConditionCheck/CalibrationConditionCheck.csproj` from the repository root.

The `speed_mps` value is the exact SI conversion of the plan's 10-knot default rounded to five decimal places. A completed run should fill `results` with software revision, achieved steady speed/heading/heel, run duration, and relevant diagnostics. Those result fields are a proposed record format, not claims that Phase 1 has measured them.

## Open contract decisions

- Decide and document the tiller sign convention when rudder behavior is implemented (Phase 3).
- Define whether `Hiking` is modeled as sailor position, righting effort, or an input mapped to both (Phase 3); keep it continuous and centered at zero.
- Define the exact frames for linear and angular velocity in concrete C# type names before implementing the state type (Phase 2).
- Select numeric wave ranges only when the physical wave model is scoped (Phase 4).
- Add model-output fields when their owning subsystem exists; avoid speculative fields for future boats or systems.
