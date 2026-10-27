# Hunter 23.5 provisional model work

This is a working engineering contract, not a calibrated model or a claim that
Phase 9 is complete. The [reference register](../phases/09-reference-progress.md)
identifies the manufacturer drawing and source limitations. It gives 3,000 lb
full-ballast displacement, 2,000 lb dry and 1,000 lb ballast, plus 23 ft 8 in
LOA, 21 ft 5 in LWL, 8 ft 4 in beam, and 236 ft² total sail area. These convert
to 7.2136 m, 6.5278 m, 2.54 m and 21.925 m² respectively. The drawing does not
establish crew/equipment inclusion or separate sail areas. We provisionally
add an 80 kg skipper to the full-ballast boat; this may double-count some
loading if the source displacement included crew. Resolve that before claiming
measurement agreement.

## Mass accounting

`Hunter235BoatDefinition` connects the provisional mass, hull, foil and rig
components to an experimental simulation. It has no published boat diagnostics
or player integration and is not yet an engineering-verified model. Coordinates use the existing hull datum and
nautical axes: X forward from the approximate waterline midpoint, Y starboard,
Z down from the nominal waterline. All allocations, centers and intrinsic
inertias below are estimates, not measurements. The dry allocations sum to
2,000 lb (907.18474 kg); ballast is separately counted once. Intrinsic tensor
off-diagonal terms are zero; the parallel-axis calculation supplies products.

| Component | kg | Center (m) | Intrinsic diagonal (kg m²) |
| --- | ---: | --- | --- |
| Hull and structure | 700 | (0, 0, -0.05) | (400, 2600, 2800) |
| Rig and sails | 100 | (0.45, 0, -2.8) | (650, 680, 45) |
| Fittings and equipment | 72.18474 | (-0.4, 0, -0.25) | (25, 100, 110) |
| Centerboard | 25 | (0.15, 0, 0.10 + 0.45 d) | (6, 7, 2) |
| Rudder | 10 | (-3.35, 0, 0.25) | (2, 2.5, 0.8) |
| Full water ballast | 453.59237 | (0, 0, 0.35) | (110, 360, 430) |
| Skipper | 80 | (-1.8, 0.55 h, -0.65) | point mass |

Here d is normalized board deployment, 0 raised to 1 lowered, and h is the
existing body-position control, -1 port to +1 starboard. The board's vertical
translation is a rough mass approximation, not a resolved pivot path. The
skipper travels between estimated cockpit seats. The ballast remains fixed
when the board moves. The 1,440.77711 kg total and inertia are computed using
`MassAggregation`; the same quasi-static internal-motion approximation described
in [boat physics](boat-physics.md) applies when connected to `Simulation`.
The mass values and inertia do not establish flotation or stability.

## Rig and geometry decisions still needed

The manufacturer drawing gives I = 25 ft 6 in, J = 8 ft 6 in, P = 25 ft 8 in
and E = 10 ft, but does not dimension the individual sail outlines. A
contemporary trial describes a full-batten main and 110% jib. Foretriangle area
is not the installed jib area. The initial geometry below is estimated and
requires further checks; the mass centers above are not a substitute for a
complete geometry or sail contract.

The port and starboard jib inputs remain independent and no automatic sheet
release is authorized. A subsequent model must define both-tight, both-eased,
slack and backwind behavior, and publish geometry and airflow from the same
completed state used for force diagnostics. The existing performance research
found no condition-complete numerical polar; further reference comparison and
calibration are Phase 11 work.

## Initial geometry for engineering implementation

The following values are provisional design choices, not digitized lines. The
hull waterplane spans X = ±3.2639 m. At each X its half-width is
`1.27 sqrt(1 - (2 X / 7.2136)²)`. The bottom of each column is at
`Z = 0.48 - 0.28 (X / 3.2639)² - 0.18 (Y / halfWidth)²` m; its sealed
height is 0.95 m. A 16 by 6 midpoint quadrature samples the hull. This geometry
is a flotation approximation, not a surveyed Hunter hull or a freeboard claim.

For future sail implementation, the starting allocation is 13.5 m² main and
8.425118 m² jib, totaling the drawing's 236 ft² after conversion and rounding.
Neither area is measured. P and E give a triangular main area of 11.923 m²;
the larger assumed main includes roach. The exact outlines, corner coordinates
and jib lead constraints still need to be defined consistently with these areas;
these tentative values alone are not an implementation-ready rig contract.

Hull resistance uses the existing local wet-column algorithm but independent,
estimated quadratic coefficients (0.016, 0.12, 0.85) and linear coefficients
(0.6, 8, 40) in body X/Y/Z. They are not towing measurements and do not model
wave-making resistance. The hydrostatic and drag checks test internal signs and
consistency only; they do not validate actual Hunter stability or performance.

Proposed sail corners in the datum above, at zero opening, are main tack
(0.5, 0, -0.65), head (0.5, 0, -8.4732), clew (-2.548, 0, -0.65);
jib tack (3.0908, 0, -0.45), head (0.5, 0, -8.2224), clew
(0.9229, 0, -0.45), all metres. The main triangular outline uses P and E;
adding a parabolic roach to the chord, `0.3024 * 4 f (1-f)` m at
fractional height f, yields approximately 13.5 m². The triangular jib's
horizontal foot and vertical height yield approximately 8.425 m². These corner
positions and outlines are inferred design geometry, not extracted drawings.
This jib is about 84% of the I × J foretriangle area, inconsistent with the
trial's reported 110% jib. Conversely, 110% of that foretriangle plus the
P × E main triangle exceeds the reported 236 ft² total before adding roach.
The quoted inventory, area convention, or dimensions therefore cannot all be
accepted as this simple geometry without further evidence. The proposed areas
are placeholders, not a verified 110% configuration.
The jib rotates around its sloping tack-to-head forestay; estimated sheet leads
are (-1.0, ±0.85, -0.35) m. The fixed leads, sheet lengths and compliance still
need an explicit dynamic constraint model before implementing sail forces.

The tentative board pivot is (0.15, 0, 0.15) m, with 1.25 m deployed span and
0.9 m² deployed area; a rudder top at (-3.35, 0, 0.10) m, 0.8 m span and
0.45 m² area is assumed. Both are rough engineering estimates and do not
reconstruct the centerboard drawing. `Hunter235Foils` uses these as vertical
wet segments with the shared provisional foil curves. Board deployment scales
span and area linearly; it does not resolve a rotating board or reproduce its
mass center's path. Tank and cockpit centers are those in the
mass table; no physical tank boundary has been established. These assumptions
provide an initial geometry envelope, subject to revision after flotation and
sail-constraint checks.

## Initial two-sail force experiment

Completed Hunter snapshots publish provisional hull dimensions, stern, foil
endpoints and skipper position in hull-datum body coordinates (X forward, Y
starboard, Z down). The board endpoint follows the force model's linearly
scaled vertical span, not a rotating board; the skipper position is shared with
the mass model. The hull height is the sampled-column height, not surveyed
freeboard. Each sail publishes tack, head, clew and leading/trailing endpoints
for its six aerodynamic sections, calculated from the same state and chord
geometry as their force and airflow diagnostics. These are engineering markers,
not surveyed sail shapes. Full boat-force diagnostics and scene validation
remain outstanding.

The `Hunter235RigModel` is a provisional reduced-order experiment. It uses six horizontal-force
sections per sail and the same smooth coefficient and luffing functions as the
ILCA sail. Main section chord follows the triangular P/E outline plus the
parabolic roach above. Jib sections follow a straight sloping forestay, with a
triangular horizontal chord taper. Jib opening rotates around the actual
forestay axis, and the apparent wind includes local boat and sail motion. Main
and jib loads and per-section airflow are returned separately and summed once.
Independent sails have no aerodynamic interaction; the Hunter definition adds
the reduced-order treatment in [Main/jib interaction](#mainjib-interaction).

Positive opening is to starboard. The port sheet limits starboard opening and
the starboard sheet limits port opening. Each limit is `0.15 + 1.3 (1 - sheet)`
radians; a linear spring acts only beyond the corresponding limit. This
restricts opening away from the centerline but does not hold the clew on the
old side during a tack. A held old sheet can therefore fail to backwind the jib;
the dynamic tack comparison in the Phase 9 record exposes this limitation.
Both tight constrains
both sides; both eased permits larger openings. The limits are compliant and
are not a geometric calculation of sheet length through the estimated leads.
Neither sheet input is automatically released. Rig inertia, stiffness and damping are
provisional; these equations have not been validated for maneuvers, backwinding,
real sail shapes or running stability.

## Isolated sheet geometry experiment

`Hunter235SheetExperiment` evaluates a straight line from the jib clew to each
estimated lead. It uses the same tack, forestay axis and foot as the current
jib geometry. The sheet is a 1,000 N/m extension-only spring: below its chosen
slack length it applies no force; above it, it pulls the clew toward the lead.
The torque is the clew force's moment about the forestay. For the port sheet,
the slack length is the distance at a nominal +1.45 rad opening when eased,
varying to the distance at -0.30 rad when fully trimmed; the starboard values
are mirrored. These endpoints and stiffness are engineering estimates, not
measured sheet lengths or a claim of correct trim response. Distance is not
monotonic over all angles, so those nominal angles are not universal opening
limits. The experiment excludes sheet damping and does not resolve hull reaction
forces at moving lead hardware.

Checks cover tension direction, mirrored forces, slack, opposing tight sheets
and agreement between torque and the spring energy gradient. A separate
fixed-hull RK4 exercise combines the experimental old-sheet torque with the
current jib aerodynamic torque, 40 kg m² inertia and 80 N m s/rad damping.
Starting at ±0.4 rad with zero rate and imposing body-frame wind (2, ∓2, 0)
m/s for 10 s, a fully held old sheet kept the jib on its starting side (minimum
opening 0.003 rad); an eased old sheet let it cross and reach ∓1.460 rad.
The opposite sheet is eased throughout. After holding for 10 s, easing the old
sheet at 0.25 per second allowed the held jib to cross, reaching ∓1.461 rad
after another 10 s. An earlier single-sheet trial with wind (3, -4, 0) m/s in
the positive-side case crossed slightly even with the old sheet held, ending at
-0.034 rad: this compliant model does not guarantee retention under arbitrary
loading.

The experiment can be selected explicitly when constructing a Hunter rig for
engineering tests. Since 2026-10-27 the standard boat definition selects it
(see [Default Hunter rig selection](#default-hunter-rig-selection)). In experimental mode the snapshot marks `ExperimentalSheetGeometry`;
the published angular limits are legacy indicators, not the geometric sheet
limits. It also publishes each sheet's clew and lead positions, line and slack
lengths, force on the clew, and torque about the forestay, evaluated at the
completed rig state and sampled input. The legacy constraint publishes no sheet
force records. Only the sheet torque is added to the jib's relative angular
equation. Equal and opposite collinear sheet forces at the clew and lead have
zero net force and moment on the combined boat; checks verify this and the
published torque. The legacy rig does not include hull reaction from relative
rig acceleration. A separate momentum-coupling experiment is described below;
both are now enabled in the standard boat definition.

### Isolated angular-momentum experiment

An engineering check isolates a single 40 kg m² jib rotational degree of
freedom, the experimental starboard sheet spring and an otherwise force-free
body with the Hunter mass tensor. Starting from rest with the jib centered and
the sheet fully trimmed, it runs for 10 s at 60 Hz without aerodynamic forces,
gravity or damping. For this reduced system, the tracked angular momentum is
`R (I omega + axis * 40 * jibRate)`, where `I` is the body tensor and `axis`
is the body-fixed forestay. With no reaction, its magnitude reaches
15.215 kg m²/s even though no external torque acts.

A test-only reaction that cancels the rate of the relative momentum keeps that
momentum near zero but changes the modeled mechanical energy by up to 0.03295 J
from 2.89359 J. A second test-only formulation couples the jib's relative
acceleration to the body's angular acceleration as well as applying the
reaction; it keeps momentum within 1e-5 kg m²/s and energy drift below 0.001 J
in this case (observed maximum 0.000197 J). These are numerical checks of a
specified simplified model, not a general articulated-rig solution.

The `experimentalRigMomentum` mode extends the trial to the boom and jib
as two body-fixed axes with constant scalar inertias of 120 and 40 kg m². It
solves their relative angular accelerations together with the hull reaction at
every RK4 stage, including external boat moments. It assumes those inertias
are already represented in the body tensor when the sails are stationary
relative to the hull. Completed rig diagnostics mark the mode and publish the
coupled derivatives. A two-sail closed-system check uses the same coupling code
with spring torques and no external forces: over 10 s, maximum momentum was
less than 1e-5 kg m²/s and energy changed by at most 0.000287 J from 18.518589 J.
A full-boat check also reconstructs the angular-momentum rate from the coupled
derivatives and verifies it equals the external moment at a completed state.
The scalar inertias, fixed axes and body tensor are estimates. The treatment
omits shape-dependent inertia and detailed moving sail mass, and the existing
mass-repositioning approximation remains in effect. These tests do not validate
a general articulated-rig solution or measured Hunter stability.

The fixed-hull exercise is not a validation of backwinded aerodynamics. A
54-second full-boat comparison with the experimental constraint, 10-knot wind
and calm water follows the steering and sheet schedule in the Phase 9 record.
The jib was at +0.381 rad at the sheet change; after 54 s it was -0.614 rad
with the old sheet released and -0.343 rad with it held. Both crossed the
upwind heading upright, ending at headings 282.8° and 250.9° respectively.
The sheet changes the result but neither run demonstrates the jib held on the
old side at the end. The full boat and its airflow also take different paths,
so this comparison alone does not isolate a backwinded aerodynamic load.

A second 54-second experimental turn starts with the starboard sheet fully
trimmed and the port sheet eased. At 20 s the jib is at +0.404 rad. One run
eases the starboard sheet at 0.25/s from 20 to 24 s; the other holds it at 1.
Both steer at -0.6 from 4 to 44 s and the port sheet stays eased. In the held
run, after the boat crosses the upwind heading while upright, the jib remains
on its starting side with aerodynamic torque below -1 N m for 335 consecutive
steps (about 5.58 s). At 54 s the held and released angles are -0.020 and
-0.639 rad, with headings 249.7° and 322.9° respectively. This demonstrates
opposing aerodynamic load during a particular simulated maneuver; it does not
validate real backwinded sail behavior, prove retention throughout the turn, or
establish equivalent boat paths. The fully trimmed and 0.65-trim tests have
different control schedules and are not a controlled trim sweep.

The held-sheet turn also has a mirrored 54-second replay: initial heading,
boom and jib angles, tiller and held sheet are reflected between sides. A
test-only wrapper supplies the mirrored initial rig; the normal Hunter
definition still starts both angles at +0.1 rad. At each step, the checks
compare reflected position, velocity, sail angles and jib torque. With the
momentum experiment enabled, the port-side case has 331 consecutive opposing-load steps after crossing the wind;
the two final jib angles are -0.020 and +0.020 rad. This is a symmetry and
internal-consistency check, not an independent real-boat observation.

With both experiments enabled, the equivalent held-sheet turn is deterministic:
the jib encounters opposing torque for 331 consecutive steps, ends at -0.020 rad
and ends at -0.643 rad when the old sheet is eased. A 60-second reach with a
-0.1 fixed tiller and starboard sheet 0.65 settles over 40–60 s at 1.892–1.896
m/s and heading 97.70–98.13°, with maximum heel 1.24° and average luff below
0.001. This is a different trim from the legacy reach; it is not a comparison
against measured performance. Thirty-second checks with seed 123, 10-knot wind,
calm/gentle/moderate water and gradual sheet, board and crew movements remain
bounded in all three presets. These checks do not establish all maneuver,
wind-range or aerodynamic-interaction requirements, nor desktop performance.
Both experiments were opt-in when these results were recorded.

## Default Hunter rig selection

On 2026-10-27 `Hunter235BoatDefinition` began constructing the rig with
sheet geometry and the two-sail momentum coupling. The legacy angular
constraint remains selectable through `new Hunter235RigModel()` for isolated
comparison checks; it is no longer used by the Godot host or scenario replays.
The `Experimental` names and flags are retained because both treatments are
still provisional, uncalibrated reduced-order models.

The two treatments assign opposite roles to the sheets. In the legacy
constraint, the port sheet limits starboard opening, so the original reach
and scripted tack trimmed the windward sheet. In the geometric model each
sheet's length is shortest near an opening on its own side, so the leeward
sheet holds the jib, as on a real boat. With the old controls (port sheet
0.65, jib to starboard), the geometric reach did not settle: over 40–60 s its
heading varied 137.65–173.98° and speed 1.191–1.487 m/s. The shared
`HunterDevelopmentScenario` therefore now starts with the starboard sheet at
0.65 and, during the tack, eases starboard and trims port. The standard reach
uses the same starboard trim.

With these changes the exact-step reach settles at 1.892–1.896 m/s, heading
97.70–98.13°, maximum heel 1.24° and average luff below 0.001, matching the
earlier coupled experimental reach. The scripted tack, board and crew replay
passes its existing crossing, filling and speed checks. Headless
`--hunter-smoke`, `--hunter-scenario-check` and `--hunter-steady-check` host
runs pass; the steady host run observed the same speed, heading, heel and luff
ranges. These are internal engineering results, not validation or desktop
performance evidence. The additional per-stage sheet and coupling cost has not
been measured on the desktop.

## Main/jib interaction

`Hunter235RigModel(sailInteraction: true)` adds a bounded strip treatment of
the airflow between the sails; the Hunter definition selects it. Each of the
six sections on one sail is paired with the section of the other sail that has
the same index, and therefore approximately the same height. Each section is
first evaluated without interaction. Its horizontal lift per unit span gives a
2D bound circulation by the Kutta–Joukowski relation,
`Γ = L' × W / (ρ|W|²)`, equal in magnitude to `½ cl c |W|`. The circulation is
placed at the section's quarter chord. Its induced velocity at the paired
section's force center is
`Γ × d / (2π (|d|² + r²))`, using the horizontal separation `d`. The core
radius `r` is an estimate: a quarter of the inducing chord, at least 0.1 m. It
keeps the induced speed finite where the jib leech overlaps the main luff.

The induced velocity is added to the paired section's apparent wind, and both
sails are then evaluated once more. This single explicit pass is deterministic
and does not iterate to a converged circulation. Each sail sees only the other
sail's induced flow; its own bound vortex and all trailing vortices are omitted.
Infinite 2D vortices over a 1.3 m strip overstate 3D interaction; no factor
corrects this. The coefficients are not fitted to wind-tunnel or sail data.
Completed snapshots publish each section's induced airflow, which is already
included in its published apparent wind.

A static check holds the boat at rest in 6 m/s of air moving aft and 0.7 rad to
port, with the boom at -0.25 rad and the jib at -0.15, -0.30 and -0.45 rad. In
every section the jib's angle of attack increases (upwash) and the main's
decreases (downwash). Induced speed stays below 0.6 m/s, within the check's
15% bound. Jib load rises and main load falls; for example, at -0.30 rad the
jib changes from 177.2 to 215.9 N and the main from 241.7 to 205.3 N. A tighter
jib produces more downwash and more luffing on the main, the trend associated
with a backwinded main. Average main luffing rises from 0.021 to 0.062, 0.051
and 0.039 for the three jib angles. Exactly mirrored inputs give mirrored
induced flow. Unloaded sails, in zero air or chord-aligned air, induce none.

With the full boat, 10-knot wind, calm water and seed 123, the standard reach
with fixed -0.1 tiller and starboard sheet 0.65 is faster with interaction:
1.962 m/s at 60 s compared with 1.892 m/s independently. It also bears away
more: 107.46° compared with 97.70°. Over 40–60 s it stays within 1.959–1.962
m/s, 106.53–107.46°, 1.24° heel and luff below 0.001. The heading spread of
0.93° is close to the existing 1° settling limit. A longer offline run
converges smoothly toward about 109.1° by 180 s, with a time constant of about
50 s; the slow directional convergence warrants attention when the helm and
balance are reviewed. The held-sheet turn retains its old-side jib under
opposing load for 323 consecutive steps; at 54 s the held and released jibs
are at -0.038 and -0.618 rad. Calm, gentle and moderate wave runs with moving
controls remain bounded, and headless smoke, scenario and steady host checks
pass. These are internal engineering comparisons, not evidence of measured
Hunter aerodynamics or performance.

## Maneuver and poor-trim checks

These checks use the standard Hunter rig (sheet geometry, sail interaction and
momentum coupling) in 10-knot north wind, calm water and seed 123. Inputs pass
through the held-input ramps; the control logic below reacts to the completed
state, so each run is deterministic.

### Tacks and jibes in both directions

Each maneuver starts beam-on with the sails on the leeward side: heading 90°
with both sails to starboard, or its exact mirror at 270° with a mirrored
initial rig. The leeward jib sheet starts at 0.65 and the mainsheet at 0.6.
From 4 s the tiller is held toward the wind (tack) or away from it (jibe)
until the boat reaches the new side's beam, then released. When the heading
crosses the wind, the old jib sheet eases and the new one trims for 3 s. For a
jibe, the mainsheet trims fully once the boat is within 30° of dead
downwind and eases back to 0.6 after the boom crosses. Without that main
trim, an exploratory jibe kept the boom on the old side until about 40° by
the lee, where the airflow first reaches the back of a main eased to that
angle.

| Maneuver | Crosses wind | Boom crosses | Beam reached | At 70 s |
| --- | --- | --- | --- | --- |
| Tack | 25.05 s | 28.85 s | 51.07 s | 1.856 m/s, 125.5° from wind |
| Jibe | 13.37 s | 16.22 s | 28.95 s | 1.523 m/s, 147.7° from wind |

The mirrored runs agree to 1e-7 in position, lateral velocity and sail
angles at every step. Both sails finish on the new side, filled (average luff
below 0.001), and maximum heel is 2.97°. With the tiller released afterward,
the boat continues to bear away, consistent with the existing need for about
-0.1 tiller on the reach.

The tack is slow: even at full tiller, the boat takes about 20 s to turn the
last 90° to the wind and falls to about 0.28 m/s near head to wind. This is a
handling observation about the provisional rudder, hull damping and balance,
not a calibrated comparison. It should be reviewed with helm balance.

### Poor trim on a held course

A deterministic PID helm (4 per rad, integral 1 per rad s with ±1 clamp,
yaw-rate damping 3 s) holds the starting course, so trim cases are compared
at the same heading. Averages are over 40–60 s.

| Trim, 100° from wind | Speed (m/s) | Luff main/jib | Max heel |
| --- | --- | --- | --- |
| Reference: main 0.6, leeward jib 0.65 | 1.922 | 0.000/0.000 | 1.28° |
| Jib eased (both sheets 0) | 1.567 | 0.000/0.565 | 0.71° |
| Leeward jib fully trimmed | 1.884 | 0.000/0.000 | 1.59° |
| Both jib sheets fully trimmed | 1.614 | 0.000/0.000 | 1.82° |
| Main eased (mainsheet 0) | 1.229 | 0.594/0.000 | 0.57° |
| Main and traveler fully trimmed | 1.820 | 0.000/0.000 | 2.06° |

On this beam reach the helm holds every case within 0.03°. Each poor trim is
slower than the reference; fully easing a sail luffs it; the fully trimmed
main heels more; two tight sheets hold the jib at 0.06 rad. At 60° from the
wind the reference main luffs (0.618) and fully trimming the main is fastest
(1.471 m/s), so these settings are not a good close-reach reference. With
both jib sheets tight at 60°, the helm saturates at full tiller and leaves a
6.73° heading error. The 60° case therefore checks only boundedness and
luffing of fully eased sails. These trends are internal consistency checks;
the absolute speeds and heel angles are provisional and not calibrated.

