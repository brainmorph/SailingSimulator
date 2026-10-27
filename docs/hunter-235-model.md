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
There is currently no main/jib aerodynamic interaction; that omission must be
addressed before Phase 9 is complete.

Positive opening is to starboard. The port sheet limits starboard opening and
the starboard sheet limits port opening. Each limit is `0.15 + 1.3 (1 - sheet)`
radians; a linear spring acts only beyond the corresponding limit, so an
old tight sheet can restrain the sail across a maneuver. Both tight constrains
both sides; both eased permits larger openings. The limits are compliant and
are not a geometric calculation of sheet length through the estimated leads.
Neither sheet is automatically released. Rig inertia, stiffness and damping are
provisional; these equations have not been validated for maneuvers, backwinding,
real sail shapes or running stability.
