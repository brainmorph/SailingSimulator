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

The new `Hunter235MassModel` is independent of the force model and is not yet
connected to a runnable Hunter. Coordinates use the existing hull datum and
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
contemporary trial describes a full-batten main and 110% jib. Their areas and
corner coordinates must be chosen as explicit estimates consistent with the
selected total and checked against feasible rig geometry before forces are
implemented. Foretriangle area is not the installed jib area. Hull lines,
tank dimensions, board pivot/outline, rudder and sheet lead coordinates also
remain estimates to be specified and verified. The mass centers above are not a
substitute for a complete geometry or sail contract.

The port and starboard jib inputs remain independent and no automatic sheet
release is authorized. A subsequent model must define both-tight, both-eased,
slack and backwind behavior, and publish geometry and airflow from the same
completed state used for force diagnostics. The existing performance research
found no condition-complete numerical polar; further reference comparison and
calibration are Phase 11 work.
