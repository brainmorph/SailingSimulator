# Visual sailing feedback

Phase 6 renders completed simulation snapshots in Godot. There is no second
trim model, render-delta integration, or Godot boat physics. The modest
procedural meshes are presentation estimates; they are not measured hull
lines, a cloth solver, or a calibrated flow visualization.

## Sail and airflow

`BoatPresentation` reads `state.Diagnostics.SailShape.Sections`. Each of the
six aerodynamic strips contributes its actual height, chord, angle, camber,
local apparent wind and luffing value. The rendered chord follows
`(-cos(angle), sin(angle), 0)` in hull-datum body axes. Its mast attachment is
reconstructed from `CenterBodyM - chord * 0.4 * ChordM`. The same model's public
height, area, mast position and foot height define the endpoints. The first
and last strip extend to the foot/head, with linearly extrapolated angle and
chord; all six original strip heights remain in the mesh. Camber makes a
parabolic chordwise belly, bulging with the local wind's normal component.
Section seams and the leech help show twist.

`SailingVisuals` adds deterministic rippling proportional to the published
`Luffing`, capped local wind speed, chord and simulation time. The leading
and trailing edges stay attached to their aerodynamic chords. This is a
visual exaggeration of poor trim: the same published luffing already reduces
the solver's sail force. Flutter does not feed forces back into physics.
Still air produces no flutter, even when the aerodynamic luffing flag is high.

Three pairs of red (port) and green (starboard) ribbons sit near the luff.
They stream along the section's local chordwise apparent flow, reverse with
reverse flow, and droop in still air. Luffing breaks up both ribbons.
A high-incidence cue adds stronger flutter on the leeward ribbon: separation
strength rises from zero to one as the absolute normal-flow fraction rises
from 0.35 to 0.80. These thresholds, ribbon dimensions and animation rates
are visual estimates, not a measured boundary-layer model. Attached force
and separated/luffing flow remain the provisional Phase 3 aerodynamics.

## Boat, water and wake

The tapered hull uses the nominal length/beam and a simplified rounded
cross-section; a dark cockpit, mast, boom and contrasting foils make attitude
and steering visible. The board and stylized sailor centres follow the exact
`IlcaMassModel` contributor positions, including raising/hiking. The rudder
uses the foil model's `-0.45 * tiller` body angle. These are geometry proxies;
their detailed volumes do not replace sampled buoyancy or mass properties.

`Water.gdshader` receives all 24 components of the active `DirectionalWaveField`
(zero in calm), including direction, amplitude, wavenumber, frequency and
seeded phase. It evaluates the same elevation and analytical normal at the
snapshot's simulation time, in world coordinates. It never uses Godot's
`TIME`, random phases or a different wave-speed setting. Physical forces
still sample the exact continuous surface at every RK4 stage.

An indexed grid has 16,641 vertices / 32,768 triangles, dense near the boat
and sparse toward an 850 m horizon. It recentres on an 8 m world grid. The
geometry between vertices approximates the continuous physical surface;
distant optical detail fades to avoid coarse-grid aliasing. Height/slope
shading makes gentle waves visible without changing their physical height.
The mesh is opaque and lightweight; underwater portions of foils are hidden
by the water. Raising a board reveals the remaining blade above deck.

`WakeTrail` retains at most 80 stern samples for seven simulation seconds,
at most one sample per 0.1 seconds. Horizontal water-relative speed below
0.25 m/s creates no new wake. Existing marks spread and fade as they age,
sample the current physical surface, and vanish after stopping. Wake marks
are visual estimates and exert no force. Repeated render reads of a snapshot
cannot spawn extra marks. Restarting a scene clears the history.

## Hunter 23.5

`HunterPresentation` draws the Hunter from each completed snapshot through the
same `SailingVisuals` cloth and telltale functions. `SailingVisuals.HunterStrips`
turns each sail's six published strips (`HunterRig.Main` and `HunterRig.Jib`
geometry plus section state) into cloth rows, adding a foot row from the
published tack to clew and a zero-length head row. Strip leading and trailing
edges are exact; belly, flutter and ribbons use each strip's own camber,
luffing and local airflow, including the other sail's induced flow. The
belly side follows the local flow, so a strip whose flow reverses bellies the
other way. The main carries three ribbon pairs at 27% chord and the jib three
pairs at 15% chord, on strips 2, 4 and 5. The boom follows the published main
tack and clew. Both jib sheets are drawn from their published lead to the clew:
yellow when loaded, grey with a sag proportional to the slack when slack.

The hull, deck, cockpit, seats, low cabin top, mast and forestay are
presentation estimates around the published length (7.21 m), beam (2.54 m)
and column heights: a fine bow, a transom of about 70% beam, the sampled
centreline bottom faired to the stem, and a sheer 0.47 m above the waterline.
They do not replace the sampled buoyancy columns. The cabin top is kept low
because the estimated main tack is only 0.65 m above the waterline; a real
Hunter 23.5 boom is higher, and the seated skipper's torso spans the boom
height when the skipper is near the centreline. The board, rudder pivot and
seated skipper follow `HunterGeometry`; the rudder uses the foil model's
`-0.45 * tiller` angle. The wake leaves the published stern with the transom
half-beam plus 0.1 m.

## Cameras and input

**C** switches views. Right drag and the wheel affect only the active camera.
Third-person orbits the rig, follows heading, keeps a level horizon and has
a 4–40 m distance range. It targets 2.1 m above the ILCA datum from 10.5 m,
or 3.4 m above the Hunter datum from 14 m; switching boats restores the
selected boat's distance. Onboard anchors to the moving sailor position with
a 0.72 m eye-height and 0.35 m aft offset on the ILCA, and to the seated Hunter
skipper with a 0.55 m eye height and 0.1 m aft offset, so the view moves
between the cockpit seats with the skipper. It rolls/pitches with the boat, has
bounded free look and a 55–95° field of view; the sailor proxy is hidden in
this view. Both retain independent look/zoom settings. Focus loss releases
mouse dragging and sailing keys. Camera code never publishes sailing input.

F2 shows/hides a controls reference window. It starts hidden, ignores key
repeat and leaves sailing/camera input active while open. The normal view
has a small controls/camera hint and no numeric instruments.
F1 contains deadline/fault messages along with [telemetry, environment
settings and performance diagnostics](debug-tools.md).

## Short review checklist

Build the Godot project, run normal sailing, and use the keys in
[the host guide](../godot/README.md). Reproducible maneuver/visual checks and
performance measurements remain Phase 8 work. Objective-data calibration
belongs to Phase 11; repeat affected visual checks after calibration changes.

- Reach on both tacks: orbit/zoom to see the full rig; change sheet/traveler
  to move the boom, then vang/outhaul/cunningham to see twist/belly change.
  Check that telltales remain readable in the onboard view.
- Ease past useful trim or head into wind: observe sail ripple, broken-up
  telltales and reduced boat drive. Compare with steady filled-sail motion.
- Steer through a tack/jibe: watch the boom cross naturally and ribbons
  change side/direction; verify camera movement does not move tiller or trim.
- Hike and steer: see body position, hull heel and horizon relationship;
  switch cameras and check that both support looking at boat/water/sail.
- Sail over gentle waves, then slow/stop: observe moving wave slopes, boat
  attitude and widening/fading wake. Raise/lower the board and inspect the
  visible blade and moving rudder from third-person.
- Toggle views, drag, zoom and lose focus: each view keeps its own settings,
  releases dragging on focus loss and leaves sailing control state alone.

For reproducible raster inspection use `--visual-review` as documented in
the host guide. It writes eighteen 1280×720 PNGs in `/tmp`. The ILCA fixtures
are both reaching sides, two luffing instants 0.07 s apart, both onboard
sides, heel/wake, downwind boom on either side and calm/stopped with a raised
board. They use seed 123 and real force diagnostics but prescribe
instantaneous body/rig states. Next come the controls window for each boat.
The six Hunter fixtures run the real model from the session start instead:
15 s reaches on both tacks (with wake), the onboard view, a tack with a
fully trimmed starboard jib sheet captured three seconds after crossing, two
seconds head to wind, and the F1 telemetry. Each prints speed, jib angle,
luffing and how many jib strips belly to windward. None of these establish
calibrated maneuvers.

The held-old-sheet tack does not backwind the jib in the current model: the
estimated 1,000 N/m sheet stretches enough under the reversed load that the
jib sits near the centreline (−0.01 rad) and all six strips belly to leeward.
The renderer agrees with that physics; a stiffer sheet estimate would be a
model change to verify in Phase 9 or calibrate in Phase 11.

Regression coverage lives in `tests/GodotIntegrationCheck` and the actual
Godot `--smoke-test`. For the Hunter, the integration check confirms the cloth
rows reproduce the published tack, clew, head and strip edges and belly to
leeward of the local flow; after each F1 restart to the Hunter the smoke
compares sampled cloth vertices of both sails with the strips and checks the
board, rudder pivot, boom, sheet endpoints, fitted camera distance, wake
width and onboard eye position. The smoke test compares every rendered sail strip to
its source geometry, verifies boom rotation sign, body/board placement,
shared wave count/time, both camera modes, focus and mouse isolation.
