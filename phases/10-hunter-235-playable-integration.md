# Phase 10: Hunter 23.5 Playable Integration

Status: started 2026-10-27 at the user's direction while Phase 9 remains open
(its desktop performance gate is paused; see
[Phase 9](09-hunter-235-physics-and-validation.md)).

Read [PLAN.md](../PLAN.md), the [phase index](README.md), and
[Phase 9: physics, verification and performance](09-hunter-235-physics-and-validation.md).

## Objective

Turn the Phase 9 Hunter model into a complete second-boat open-practice
experience, with boat selection, manual jib controls, readable sailing cues
and both camera views. Preserve the model's engineering-verified behavior and
meet stable 60 FPS presentation and fixed 60 Hz simulation cadence in the finished scene.
Physics fidelity and smooth performance take priority over graphic detail.

## Depends on

Phase 9, including its reference record, engineering verification, minimal
Godot scene, desktop timing measurements and completion record. Do not use
this phase as the destination for unresolved core-physics or timing defects.

Objective-data calibration is deferred to
[Phase 11](11-calibration-and-realism-validation.md) for both boats. Missing
compatible measurements or calibrated-realism evidence do not block this
phase. The completed experience is a playable prototype with provisional
physics, not the calibrated simulator milestone.

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
   If a model change is needed, rerun the affected Phase 9 verification and timing.
3. Review real-time tacks/jibes, both-sheet errors, crew movement and board
   deployment through visible cues in calm and seeded waves, in both cameras.
4. Repeat desktop performance measurements with the completed Hunter geometry,
   two sails, telltales, wake, both camera modes and F1/F2 usage, including panel
   opening and boat switches. Record frame-time distributions, stalls, worker
   deadlines, allocations and GC using the established protocol.
5. Require stable 60 FPS presentation and fixed 60 Hz simulation cadence. Phase
   9's minimal-scene results do not replace these measurements. Reduce visual
   cost where needed while keeping essential sailing cues and verified physics;
   unresolved performance defects keep this phase open.
6. Retain ILCA regression and desktop checks alongside the Hunter evidence.

## Progress

**10.1 boat selection and session lifecycle (implemented).** `BoatCatalog` and
`SailingSession` in the adapters project own the boat list, each boat's initial
controls and the restart policy. A restart publishes released input, stops and
joins the previous worker, then creates a fresh simulation with the retained
environment object (wind, waves, seed) at the common beam-reach start. The
Hunter starts with main 0.7, leeward (starboard) jib sheet 0.7 and the skipper
on the windward seat. The main scene shows a picker before any simulation runs;
`--boat=ilca-7` or `--boat=hunter-235` skips it, and automated ILCA checks
start without it. F1 has a Session tab with a boat selector and restart button.
Restarts release held keys and clear F1 graphs and wake history. The wake now
starts at the Hunter's published stern. F1 telemetry shows Hunter forces per
component, jib angle, sheets and luffing in place of the ILCA sail strips.
The Hunter uses `HunterPresentation`, the Phase 9 block geometry shared with
the development host; recognizable geometry, cues and fitted cameras remain 10.3.

Verification: an integration group restarts sessions four times across both
boats, checking the joined worker, step-zero state, retained environment,
reset controls, rig and pose, that stale input cannot reach the stopped worker,
and released input in the new session. The Godot smoke switches three times
through F1 and checks the scene state listed in the Godot README; a picker
check confirms no session before a choice. All existing test projects and the
Hunter development checks pass. Hunter jib-sheet bindings (10.2) are not yet
implemented, and desktop review and performance measurement of the switched
scene remain outstanding.

**10.2 player controls and help (implemented).** At the user's direction, O/L
trim and ease the port jib sheet and P/; the starboard sheet, at the Phase 9
held-input rate of 0.25/s. They are physical-key bindings like the others and
are ignored on the ILCA. The existing Z/X crew keys move the Hunter skipper
between its estimated cockpit seats and I/K drive its centerboard. The Hunter
model uses every main control (mainsheet, traveler, vang, outhaul and
cunningham), so all remain offered. F2 lists only the selected boat's controls,
with a note that sheets are never handled automatically. No sheet release or
tack assistance was added.

Verification: the scene smoke checks both jib-sheet key pairs alongside the
others (positive, opposed and negative), and after each F1 restart checks the
F2 rows, that held jib keys ramp gradually on a running Hunter session and
leave the ILCA unchanged, and that focus loss releases them. Help and camera
isolation checks are unchanged. The visual review adds a Hunter controls-window
capture. A new offline group starts from the player's Hunter session (initial
controls, start heading, default environment with seed 123) and tacks using
only full key directions through the held-input ramps: A held until the wind
crosses, then ; and O and X for three seconds, then the tiller released at the
new beam. It crosses the wind at 17.7 s and reaches the new beam at 30.8 s; at
70 s it sails at 2.41 m/s with both sails filled and a maximum heel of 9.5°.
With the tiller released the boat then bears away to about 139° from the wind,
consistent with the weak directional stability recorded in Phase 9.

## Deliverables and completion criteria

- Both boats selectable through the initial picker and F1 with consistent restart.
- All agreed Hunter controls implemented and documented in F2/user documentation.
- Recognizable Hunter with readable main/jib cues, moving cockpit skipper,
  adjustable board, correct wake origin and both camera modes.
- F1 diagnostics use the same per-sail completed state as physics and rendering.
- Reproducible end-to-end and ILCA regression evidence, with continued agreement
  with the Phase 9 engineering verification record.
- Measured stable 60 FPS and fixed 60 Hz cadence in the completed experience on
  a documented representative desktop, with no unresolved milestone defects.
- Updated README, launch/controls, integration and visual-feedback documentation,
  plus a concise completion record with known data/model limitations.
- A Phase 11 handoff retains both boats' reproducible scenarios and explicit
  provisional parameters/data gaps. Missing calibration data is not an unmet
  Phase 10 engineering criterion and does not prevent completion.
