# Phase 10: Hunter 23.5 Playable Integration

Status: upcoming planning draft. Implementation has not started.

Read [PLAN.md](../PLAN.md), the [phase index](README.md), and
[Phase 9: physics, validation and performance](09-hunter-235-physics-and-validation.md).

## Objective

Turn the Phase 9 Hunter model into a complete second-boat open-practice
experience, with boat selection, manual jib controls, readable sailing cues
and both camera views. Preserve the model's validated behavior and meet stable
60 FPS presentation and fixed 60 Hz simulation cadence in the finished scene.
Physics fidelity and smooth performance take priority over graphic detail.

## Depends on

Phase 9, including its reference record, physics/calibration evidence, minimal
Godot scene, desktop timing measurements and completion record. Do not use
this phase as the destination for unresolved core-physics or timing defects.

## Confirmed scope

- ILCA 7 and Hunter 23.5 are selectable before sailing and through F1.
- Switching through F1 restarts the sailing session with the selected boat.
- Hunter port/starboard jib sheets are eased and trimmed independently.
- Ballast stays full; the centerboard is adjustable during sailing.
- Full mainsail and jib; reefing and furling remain deferred.
- The skipper can move between port and starboard cockpit seats.
- Normal sailing relies on visual cues; F1 provides detailed development data.

## Work

### 10.1 Boat selection and session lifecycle

1. Add a pre-session boat picker and an F1 boat selector. Initialize the chosen
   model, mass/rig state, pose, controls and presentation as one consistent session.
2. Follow this proposed restart policy: retain current wind/wave settings and
   wave seed for comparisons; reset simulation time, boat motion, rig and sailing
   controls. Release held inputs and clear telemetry/wake history.
3. Stop and join the old simulation worker before replacing it. Ensure stale
   input or snapshots cannot affect the new session; repeat switches both ways.

### 10.2 Player controls and help

1. Choose nonconflicting keyboard bindings for four jib-sheet actions: trim and
   ease for each side. Use the Phase 9 control contracts and gradual input ramps.
2. Bind crew movement to the Hunter cockpit path and board control to the
   Hunter model. Offer only main controls supported by its reference configuration.
3. Show applicable controls in F2 for the selected boat. Verify held inputs,
   opposed keys, focus loss and camera input isolation, including panel use.
4. Confirm that ordinary steering and sheet handling still produce maneuvers;
   the interface must not add automatic sheet release or scripted tack assistance.

### 10.3 Boat presentation, sailing cues and cameras

1. Build recognizable modest-detail Hunter hull, cockpit, mast, main, jib,
   foils and skipper geometry using the Phase 9 boat definition.
2. Drive both visible sails, luffing and telltales from their published geometry
   and local airflow. Backwinding and poor trim must remain visible and agree
   with the physics; avoid a second independent trim model in the renderer.
3. Render crew/board positions from the mass/foil model, with a wake originating
   at the Hunter stern. Remove remaining ILCA-specific geometry assumptions.
4. Fit onboard and third-person cameras to Hunter dimensions. Onboard position
   follows the published skipper location as the skipper changes seats.
5. Integrate per-sail forces, shape, airflow and sheet states into F1, with totals
   that count each sail once. Keep detailed implementation data in development UI.

### 10.4 End-to-end regression and performance

1. Verify both selection paths, repeated session restarts, worker shutdown and
   the retained/reset state policy. Check controls/help, cameras, snapshot
   ownership and geometry/force agreement for both boats.
2. Replay Phase 9 scenarios with identical inputs/environment. Presentation and
   selection changes must not introduce unexplained differences in the physics.
   If a model change is needed, rerun the affected Phase 9 validation and timing.
3. Review real-time tacks/jibes, both-sheet errors, crew movement and board
   deployment through visible cues in calm and seeded waves, in both cameras.
4. Repeat desktop performance measurements with the completed Hunter geometry,
   two sails, telltales, wake, both camera modes and F1/F2 usage, including panel
   opening and boat switches. Record frame-time distributions, stalls, worker
   deadlines, allocations and GC using the established protocol.
5. Require stable 60 FPS presentation and fixed 60 Hz simulation cadence. Phase
   9's minimal-scene results do not replace these measurements. Reduce visual
   cost where needed while keeping essential sailing cues and validated physics;
   unresolved performance defects keep this phase open.
6. Retain ILCA regression and desktop checks alongside the Hunter evidence.

## Deliverables and completion criteria

- Both boats selectable through the initial picker and F1 with consistent restart.
- All agreed Hunter controls implemented and documented in F2/user documentation.
- Recognizable Hunter with readable main/jib cues, moving cockpit skipper,
  adjustable board, correct wake origin and both camera modes.
- F1 diagnostics use the same per-sail completed state as physics and rendering.
- Reproducible end-to-end and ILCA regression evidence, with continued agreement
  with the Phase 9 physics/calibration record.
- Measured stable 60 FPS and fixed 60 Hz cadence in the completed experience on
  a documented representative desktop, with no unresolved milestone defects.
- Updated README, launch/controls, integration and visual-feedback documentation,
  plus a concise completion record with known data/model limitations.
