# Sailing Simulator Plan

Implementation work is broken into actionable, ordered handoffs in [phases/README.md](phases/README.md).

## Vision

Build a sailing simulator in C# with Godot, aiming for physics fidelity comparable to eSail Sailing Simulator. Use eSail as a realism benchmark, not as the source of boat specifications: this project starts with an ILCA 7. The simulation should behave realistically enough to let a sailor practice boat handling and sail trim and develop a feel for the boat.

## Confirmed decisions

| Area | Decision |
| --- | --- |
| Dependencies | Do not add third-party libraries. |
| Data acquisition and workarounds | Do not undertake operations that download large amounts of data or require complex workarounds without warning the user first and getting their direction. |
| Engine and language | Godot and C# |
| World representation | Full 3D simulation and presentation |
| Initial input | Keyboard and mouse |
| Input roles | Keyboard controls sailing; C switches camera view; mouse controls look and zoom |
| Input handoff | Godot publishes a thread-safe snapshot of current sailing control states for the simulation thread to sample each step |
| Input response | Hold keys for smooth, gradual adjustments |
| Key binding settings | Fixed defaults in the first playable version; no rebinding UI initially |
| Control reference | F2 toggles an in-game controls window; sailing continues while it is open |
| Fixed sailing keys | A/D tiller; W/S sheet; Q/E traveler; R/F vang; T/G outhaul; U/J cunningham; I/K centerboard; Z/X hiking. See [rates and directions](godot/README.md). |
| Initial boat | Single-handed ILCA/Laser dinghy |
| Initial rig reference | ILCA 7 |
| Initial training focus | Boat handling and sail trim |
| Intended audience | New and experienced sailors |
| Initial session format | Open practice; no structured drills required initially |
| Beginner guidance | None in the first version; rely on visual cues and boat response |
| Initial wind | Steady wind; develop more wind models later |
| Initial wind settings | Default to 10 knots; allow the player to adjust steady wind speed and direction |
| Wind speed range | 0–30 knots, set with an F1 debug-menu slider |
| Wind direction control | Set steady-wind direction through the F1 debug menu |
| Wind direction input | 0–360° slider with compass labels |
| Wind direction convention | Angle indicates where wind comes from; 0° is wind from the north |
| Wind speed control | Adjustable slider in the F1 debug menu, alongside wind direction |
| Wind calculation | Uniform steady true wind; calculate apparent wind from true wind and boat motion |
| Initial water | Gentle waves |
| Water current | Still water initially; add current later |
| Initial wave pattern | Irregular |
| Wave generation | Directional wave spectrum with seedable realization for repeatable conditions |
| Wave direction | Align with wind by default; allow independent adjustment |
| Wave direction input | Independent compass-labeled 0–360° slider in F1 |
| Wave dynamics | Waves affect boat movement from the start |
| Wave handling effects | Waves influence speed and steering feel in a practical force-based model |
| Wave repeatability | Varied by default, with repeatable conditions available for comparisons |
| Wave settings | Default to gentle waves; allow the player to adjust wave strength |
| Wave control location | F1 debug menu, alongside wind settings |
| Wave controls | Calm, gentle, and moderate presets; gentle is the default |
| Player feedback | Visual cues only during normal sailing, to encourage feel rather than instrument watching |
| Visual sailing cues | Sail behavior and boat/water response together |
| Telltales | Include visible telltales responding to airflow |
| Sail feedback | Control inputs affect visible sail shape, twist, and luffing as well as force |
| Poor-trim feedback | Clear sail luffing accompanies reduced sail force |
| Visual quality priority | Physics fidelity and smooth simulation take priority; modest graphics are acceptable |
| Camera | Switchable onboard and third-person views with C; independent look/zoom settings |
| Debugging | F1 toggles a detailed display of simulation numbers |
| Debug display | Numeric values and live graphs |
| Controls | Tiller, mainsheet, traveler, vang, outhaul, cunningham, and adjustable centerboard position |
| Maneuvers | Tacks and jibes emerge from player controls and simulation physics |
| Body position | Continuous hiking/righting-effort control, adjusted gradually by holding a key |
| Reference sailor mass | Fixed 80 kg baseline, provisional; derived from the midpoint of ILCA Australia's 75–85 kg ideal ILCA 7 sailor range |
| Working boat mass budget | Treat the 58 kg nominal reference as the complete equipped boat, distribute it across hull, rig, foils, and fittings, and add the 80 kg sailor separately; verify against class measurement rules and study assumptions |
| Centerboard control | Player can raise and lower the centerboard during sailing |
| Hiking physics | Shift the sailor's center of mass to produce a righting moment |
| Sailor mass representation | One sailor mass point whose position shifts with the continuous hiking control |
| Mass properties | Represent hull, rig, foils, and sailor as separate mass contributors so their positions affect boat balance and motion |
| Physics approach | Hybrid: force-based behavior tuned against reference data |
| Simulation ownership | C# calculates forces and integrates all sailing motion; Godot's physics engine does not drive the boat |
| Simulation packaging | Standalone .NET library, independent of Godot |
| Physics units | SI units internally; convert to knots and degrees at user-facing boundaries |
| Coordinate convention | Nautical body axes in the C# core (+X forward, +Y starboard, +Z down); convert to Godot axes at the rendering boundary |
| Orientation representation | Quaternions internally; derive heel, pitch, and heading for display |
| Godot's simulation role | Render the boat, water, and environment from C# simulation state |
| Boat motion | Full six-degree-of-freedom motion: forward, sideways, vertical, roll, pitch, and heading |
| Underwater foils | Model centerboard and rudder as separate lift-and-drag force sources |
| Hull/water interaction | Compute buoyancy and drag at multiple hull points against the wave surface |
| Hull geometry | Simplified measured/parametric hull surface with sampled buoyancy and drag points; refine if better geometry data becomes available |
| Sail aerodynamics | Compute forces across multiple sail sections to represent twist |
| Sail force response | Trim changes affect drive, drag, and sideways force |
| Sail shape model | Reduced-order sail shape and twist driven by trim and wind; use the same shape for aerodynamic forces and rendering |
| Simulation timing | Fixed physics timestep, independent of rendering |
| Physics update rate | Hard requirement: fixed 60 Hz wall-clock cadence |
| Simulation threading | Run C# simulation on a separate thread; publish synchronized state for Godot rendering |
| Snapshot rendering | Render the latest completed simulation snapshot directly initially; retain the option to add interpolation if needed |
| Simulation slowdown handling | Soft warning after 3 consecutive late steps; hard warning after 5; no catch-up, dropped-time, or slow-motion workaround initially |
| Performance diagnostics | F1 includes timing percentiles, subsystem timings, allocations, and garbage-collection metrics in addition to late-step warnings |
| Time integration | RK4 at the initial 60 Hz physics rate |
| Performance target | Stable 60 FPS on a typical desktop |
| Calibration approach | Compare against compatible published measurements; use reproducible behavior checks and document data gaps and estimates |
| Performance target | Use published ILCA 7 VPP polars as initial speed and pointing targets, with conditions and assumptions recorded |
| Performance data sources | Include the 2016 Laser-dinghy study as a candidate alongside the 2022 ILCA 7 study, subject to compatibility checks |
| Validation scope | Published reference comparisons, reproducible numerical/behavior checks, visual consistency checks, and desktop performance measurements |
| Deferred areas | Racing and capsize recovery are not initial priorities |

## Design goals

- Make steering, sail trim, and heel respond in ways that support meaningful practice.
- Drive visible sail shape, twist, and luffing cues from the same control state used to calculate sail forces.
- Render telltales responding to local airflow as a visual trim cue.
- Make under-trimming visible through luffing and reduced sail force.
- Prioritize steering, tacking, jibing, and stopping, along with sail trim and upwind/downwind wind-angle practice.
- Produce tacking and jibing from ordinary steering and sail controls rather than scripted maneuver actions.
- Prioritize physics fidelity and smooth performance over high-end graphics; readable low-fidelity visuals are acceptable.
- Teach through the boat's visible behavior and response, not normal-play gauges or numeric readouts.
- Keep a detailed F1 overlay available for development and debugging.
- Start with a controlled, steady-wind environment while leaving room for richer wind models later.
- Calculate apparent wind from the uniform true wind and the boat's motion.
- Expose the steady-wind direction control through the F1 debug menu, not the normal sailing view.
- Use a 0–360° wind-direction slider with compass labels in the F1 debug menu.
- Interpret the direction angle as the compass direction the wind comes from (0° = from north); convert it to the C# wind velocity vector.
- Expose steady-wind speed as a slider in the F1 debug menu alongside the direction control.
- Use a 0–30 knot range for the adjustable steady-wind speed, with a 10-knot default.
- Include gentle water motion in the initial environment.
- Generate irregular waves from a directional wave spectrum; seed wave realizations so selected conditions can be repeated exactly.
- Align waves with wind by default, while allowing their direction to be adjusted independently.
- Expose independent wave direction through a compass-labeled 0–360° slider in the F1 debug menu.
- Expose the adjustable wave-strength setting through the F1 debug menu alongside the wind controls.
- Offer calm, gentle, and moderate wave presets, with gentle as the default.
- Assume still water with no current in the initial environment; calculate boat speed relative to this water state.
- Let waves influence handling without requiring a full computational fluid dynamics model.

## Architecture

### Confirmed

- The project will use C# with Godot.
- Boat behavior will use a force-based model tuned against reference data.
- Boat physics will run at a fixed timestep independent of rendering.
- Maintain a hard fixed simulation cadence of 60 Hz in wall-clock time, independent of rendering. A sustained rate below 60 Hz is a performance defect to diagnose and fix.
- Start with fourth-order Runge–Kutta (RK4) integration at 60 Hz; verify stability and performance during implementation before considering substeps.
- Run simulation stepping on a dedicated C# thread, separate from Godot rendering; define safe input handoff and state-snapshot publication boundaries.
- Render the latest completed state snapshot directly at first to minimize visual latency; keep the interface open to adding interpolation if visible motion requires it.
- Pass sailing controls from Godot to C# as a thread-safe current-state snapshot that the simulation samples each fixed step.
- Monitor step deadlines and report sustained misses. Do not add a catch-up, dropped-time, or slow-motion policy as a workaround unless the design is explicitly revisited.
- Define a late step as a simulation step completing after its 60 Hz deadline; show a soft warning after 3 consecutive late steps and a hard warning after 5 in the F1 diagnostics.
- Include rolling step-time percentiles, per-subsystem timings, managed allocation rates, and garbage-collection activity in F1 performance diagnostics.
- C# will calculate forces and integrate sailing motion; Godot's physics engine will not drive boat dynamics.
- The C# simulation will be a standalone .NET library with no Godot dependency.
- Use SI units internally in the simulation library; convert to knots and degrees for user-facing settings and displays.
- Use a right-handed nautical body frame for the simulation core (+X forward, +Y starboard, +Z down); convert coordinates at the Godot adapter boundary.
- Represent 3D orientation internally with quaternions; derive human-readable heel, pitch, and heading angles for telemetry and presentation.
- Phase 2 world frame is north/east/down (+X north, +Y east, +Z down). Position and linear velocity are world-relative; angular velocity is body-relative. Double-precision Hamilton quaternions map body axes into world axes; see [the core API](docs/simulation-core.md).
- Phase 3 implements a provisional flat-water ILCA 7 model with sampled buoyancy/drag, separate foil forces, six sail sections and a dynamic boom. All estimated mass allocations, geometry and coefficients are documented in [the boat physics model](docs/boat-physics.md); they are not measured calibration data. Positive tiller turns to starboard in forward flow; positive hiking places the sailor to starboard.
- Phase 3 combines the provisional 58 kg equipped budget and separate 80 kg sailor through full mass-weighted centres and inertia tensors. Internal repositioning preserves the combined centre of mass and angular momentum at the step boundary, using a documented quasi-static approximation. The class-rule review did not resolve the 58 kg removable-equipment interpretation; it remains an open calibration issue.
- Phase 4 adds an immutable seeded directional linear-wave spectrum, sampled at each RK4 stage for hull/foil immersion and local water-relative flow. Calm is flat/stationary; gentle and moderate use provisional spectral significant heights of 0.12 m and 0.30 m, with 3–12 m wavelengths and ±25° directional spread. See [wind and waves](docs/wind-and-waves.md) for equations, assumptions and rendering access. These values are design estimates pending calibration.
- Environment creation chooses a random wave seed when omitted; an explicit integer reproduces a realization. Preserve that seed when editing settings so slider changes do not reshuffle the wave phases. Physics and rendering sampling consume no randomness.
- Godot will render the state produced by the C# simulation.
- Phase 5 implements the [Godot host and thread boundary](docs/godot-integration.md): immutable held-key/environment publication, fixed-step control ramps, completed state/timing snapshots, absolute 60 Hz deadlines, visible misses/faults, and joined shutdown. The root solution stays engine-independent; build the Godot 4.7 .NET project separately. Render meshes use the hull datum, offset from the published combined centre of mass.
- Phase 6 implements [visual feedback and cameras](docs/visual-feedback.md): sectional sail camber/twist and wind-gated luffing from aerodynamic diagnostics; paired airflow telltales; mass-following sailor/board and rudder meshes; shared-spectrum wave rendering and a bounded, fading wake. C switches a heading-following third-person orbit and a body-following onboard view; mouse look/zoom never changes sailing input. Ribbon separation thresholds and wake/cloth animation are presentation estimates checked for consistency with simulation state. Water geometry approximates the continuous field between vertices.
- Phase 7 implements [F1 debug tools and performance](docs/debug-tools.md): environment sliders/presets, signed seeds and session-local saved conditions; completed-state telemetry, force/moment breakdowns and bounded live graphs; 300-step compute percentiles, nested subsystem timing, worker allocations, process GC/heap and render FPS. Full current-step/deadline timing includes metrics collection. The original absolute 60 Hz schedule and 3/5-miss warnings remain unchanged. Environment edits retain the seed; restoring conditions does not rewind boat state or time.
- The simulation will calculate all six degrees of boat motion.
- Centerboard and rudder forces will be modeled separately from hull drag.
- Sample buoyancy and drag along the hull so waves can affect all six degrees of motion.
- Use a simplified parametric hull surface grounded in measured dimensions, with sampled locations for buoyancy and drag; refine the geometry as better lines data becomes available.
- Compute sail forces by section so sail twist can affect the force distribution.
- Calculate reduced-order sail shape and twist from trim and wind state, and use the same representation for force calculations and rendered shape.
- Let sail trim change forward drive, drag, and sideways force.
- Model hiking by shifting sailor mass position to produce a righting moment.
- Represent the sailor as one movable mass point in the initial physics model; hiking input shifts that point continuously.
- Keep hull, rig, foils, and sailor as separate mass contributors in the physics model; establish component masses and centers of mass from measured data where available and record approximations for missing values.
- Use compatible published measurements as the calibration baseline; use reproducible scenarios to check behavior and keep unsupported values explicitly provisional.
- Use published ILCA 7 VPP polars as initial quantitative speed and pointing targets, recording the underlying assumptions and conditions.
- Target stable 60 FPS on a typical desktop.
- Complete calibration through published-data comparisons, reproducible simulation/visual checks and performance measurements. Subjective sailing feel remains unassessed and is not a milestone requirement.

### Working proposal (to review)

- Give the standalone simulation library an explicit interface for control inputs, configuration, and output state for rendering.
- Represent the boat, wind, and water as explicit simulation state so additional boat classes and environment models can be added later.
- Make the F1 debug display read the same simulation state that drives the boat, so it can expose forces, angles, control inputs, and resulting motion as those values are defined.

These are starting points for discussion, not locked implementation decisions.

## Initial scope

1. Sail an ILCA/Laser in open practice.
2. Steer with the tiller and adjust the mainsheet, traveler, vang, outhaul, cunningham, and centerboard.
3. Control heel with a simplified hiking input.
4. Read wind and boat behavior from visual cues.
5. Sail in steady wind over gentle waves.
6. Toggle detailed numeric telemetry with F1 while debugging.

## First playable milestone

Deliver the full initial scope as the first playable milestone: ILCA 7 open practice with the selected controls, visual feedback, steady wind defaulting to 10 knots, gentle irregular waves that affect boat motion, adjustable wind and wave settings, repeatable conditions for comparisons, and switchable onboard and third-person cameras.

## Calibration sequence

Phase 8 is active. [Free-sailing baselines](docs/calibration.md) record exact
replay, setup/build metadata and final-window stability screening. These are
internal observations, not reference polars. Current [milestone evidence and
remaining work](phases/08-progress.md) retain the unresolved desktop cadence
defect and missing compatible numeric targets; no physics parameters have
been calibrated in this pass.

1. Establish steady-sailing behavior under a consistent wind and repeatable water conditions.
2. Calibrate reaching and sail trim across wind angles.
3. Calibrate tacking and jibing transitions.

## Reference data

### ILCA 7 dimensions

The [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf) lists the following baseline specifications (p. 14):

| Measurement | Value | Notes |
| --- | ---: | --- |
| Length overall | 4.23 m | Class reference |
| Length at waterline | 3.81 m | Class reference |
| Beam | 1.37 m | Class reference |
| ILCA 7 sail area | 7.06 m² | Rig reference |
| Nominal weight (with fittings) | 58 kg | Working model assumption: complete equipped boat mass, distributed among hull, rig, foils, and fittings; interpretation must be checked against class measurement rules |

ILCA Australia gives an ideal ILCA 7 sailor weight range of 75–85 kg and a typical range of 70–90 kg on its [class association page](https://ilca.au/about-us/). Use 80 kg as a provisional fixed reference sailor mass. This is the midpoint of class guidance, not a universal class rule or a measured mass from the Chalmers force dataset.

### Force and performance research

- [Magnander and Larsson, Chalmers/RISE, 2023](https://research.chalmers.se/en/publication/546299) publish wind-tunnel sail-force and moment coefficients for the ILCA 7 MkII sail. The study covers full-scale apparent wind speeds of 4 m/s and 7 m/s and a range of sheet, kicker, apparent-wind-angle, and heel conditions. The associated dataset is public; the measurements focus on downwind sailing, so they are a partial calibration source rather than a complete sailing polar.
- [Magnander, Chalmers Master's thesis, 2022](https://odr.chalmers.se/items/edcb1ca9-d053-486d-b3e9-a9dafd7d6d6b) combines those sail coefficients with hydrodynamic coefficients from 2014 full-scale towing-tank tests to produce a velocity-prediction model. Its stated focus is downwind sailing in flat water, so it is not a complete upwind/reaching target set. Numeric polar coordinates and crew-mass assumptions have not yet been extracted and verified; do not use assumed values as calibration data.
- [Levin, Chalmers licentiate thesis, 2016](https://research.chalmers.se/en/publication/245846) reports predicted velocity and attitude for a Laser dinghy across wind speeds and directions, with emphasis on maximum upwind and downwind VMG. Include it as a candidate complementary source alongside the 2022 study; numeric results, conditions, rig/crew assumptions, and compatibility with the selected ILCA 7 baseline still need review.
- The separate [Pennanen et al. 2016 Laser heel-and-trim study](https://research.chalmers.se/publication/234579) reports flat-water upwind optimum heel/trim at selected boat speeds (2–5 knots). Its narrow points are useful for heel/trim trend checks, not as a speed polar; the Laser rig/model and crew-mass assumptions still need compatibility review.
- The [2017 Laser dinghy VPP study](https://www.sciencedirect.com/science/article/pii/S0029801817300938) compares crew weights against an 80 kg baseline and evaluates upwind performance against measured data. Treat this as a supplementary crew-weight and validation reference, not a target set to merge with the ILCA 7 sources.

## Data points still to establish

- Extract numeric VPP polar targets from the 2022 downwind study and/or 2016 Laser-dinghy study; record their exact conditions and model assumptions before calibration.
- Verify whether the 80 kg reference sailor mass is compatible with the crew assumptions in the selected velocity-prediction data. A separate 2017 Laser VPP uses 80 kg as its baseline crew, but that does not establish compatibility with the 2022 ILCA 7 model.
- Verify the working interpretation of the handbook's 58 kg nominal weight (with fittings) against class measurement rules and the selected VPP assumptions; avoid double-counting the rig or removable gear.
- Wind speed/direction ranges are resolved: 0–30 knots (10 default), 0–360° from-bearing. Phase 4 verifies their adapter conversion; Phase 7 supplies F1 controls.
- Expected boat-speed and pointing targets at representative upwind, reaching, and downwind angles, with source conditions recorded. The identified 2022 VPP source is downwind-focused; locate or extract suitable upwind/reaching data.
- Phase 4 selects provisional wave height, wavelength and spread ranges in [the environment model](docs/wind-and-waves.md). Check these estimates against compatible published sea-state data where available and reproducible handling scenarios; retain unsupported values as estimates.
- Water current model and settings (deferred from the initial environment).
- Any reliable upwind or broad-reach ILCA 7 performance data beyond the identified studies.

Record values with units and source or rationale. Keep experimental conditions alongside each value so comparisons use compatible wind, trim, and water assumptions.

## Open questions

- The initial sectional sail, centerboard, rudder and hull equations and provisional coefficients are recorded in [the Phase 3 model](docs/boat-physics.md), extended with [Phase 4 waves](docs/wind-and-waves.md). Which measured coefficients should replace or calibrate them?
- Extract and verify numeric ILCA 7/Laser VPP polar values, conditions, and crew/rig assumptions before calibration; current sources do not yet provide a verified numeric target table.
- Wind and wave settings use the confirmed knots/compass angles and calm/gentle/moderate presets. Phase 7 implements F1 editing, seed selection and session-local saved conditions; usability remains subject to Phase 8 review.
- The initial fixed keys and adjustment rates are documented in [the host controls](godot/README.md). Assess their usability during visual review and calibration.
- Phase 6's low-fidelity renderer now shows shared sail shape, telltales, heel, wake and waves; check cue visibility, camera framing and consistency with simulation state through reproducible visual scenarios during calibration. Presentation heuristics and the review checklist are recorded in [visual feedback](docs/visual-feedback.md).

## Deferred scope

- Racing, starts, marks, and racing rules.
- Capsize and recovery.
- Variable wind, gusts, and shifts.
- Additional boats and structured training drills.
- Tutorials, hints, and guided lessons.
