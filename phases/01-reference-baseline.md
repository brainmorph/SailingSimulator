# Phase 1: Reference and Calibration Baseline

Review date: 2026-10-22

This is the source-backed baseline gathered during the initial Phase 1 pass. Numeric values below are usable only within their stated conditions. No unreported component masses, centers of mass, or polar values are inferred.

## Boat and sailor references

| Quantity | Value | Source and location | Use and limitation |
| --- | ---: | --- | --- |
| Length overall | 4.23 m | [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf), p. 14 | Class reference dimension. |
| Length at waterline | 3.81 m | [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf), p. 14 | Class reference dimension. |
| Beam | 1.37 m | [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf), p. 14 | Class reference dimension. |
| ILCA 7 sail area | 7.06 m² | [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf), p. 14 | Rig reference. |
| Nominal weight with fittings | 58 kg | [2025 ILCA Handbook](https://ilcasailing.org/laserinternational/wp-content/uploads/2025/02/HANDBOOK-2025.pdf), p. 14 | The handbook label does not establish which removable items are included. Keep the plan's complete-equipped-boat interpretation provisional; verify it against class measurement rules before allocating mass. |
| Ideal ILCA 7 sailor weight range | 75–85 kg | [ILCA Australia class information](https://ilca.au/about-us/) | Guidance, not a class rule or a mass assumption in the cited research. |
| Typical ILCA 7 sailor weight range | 70–90 kg | [ILCA Australia class information](https://ilca.au/about-us/) | Guidance, not a class rule. |
| Reference sailor mass | 80 kg | Midpoint of ILCA Australia's ideal range; also used as the baseline crew in a separate Laser VPP study (see below) | Provisional modeling choice. This cross-check does not establish compatibility with the 2022 ILCA 7 VPP or wind-tunnel dataset. |

No source-backed breakdown of the 58 kg boat mass into hull, rig, foils, and fittings, or of component centers of mass, was established in this pass. Those remain estimates/open questions and must not be silently treated as measured values.

Phase 3 supplies an explicitly provisional component budget, centres and
inertias in [the implementation model](../docs/boat-physics.md), totaling the
plan's 58 kg plus the separate 80 kg sailor. Its small current-class-rule
review did not establish a removable-equipment mass breakdown. These estimates
do not change the source-backed baseline or resolve VPP compatibility.

## Aerodynamic and performance sources

| Source | Extracted facts | Calibration use and limits |
| --- | --- | --- |
| Magnander & Larsson, [Wind-Tunnel Investigation of the ILCA 7 MKII Sail in Downwind Conditions](https://research.chalmers.se/publication/546299), *Journal of Sailing Technology* 8 (2023), pp. 118–142, DOI 10.5957/jst/2023.8.7.118 | Wind-tunnel coefficients cover six force/moment degrees of freedom. Full-scale apparent-wind cases are 4 m/s and 7 m/s. The 4 m/s case covers five apparent-wind angles, seven sheet angles, three kicker trims, and three heel angles. The 7 m/s case is upright and dead downwind, with four sheet angles and three kicker trims. The paper gives force-coefficient normalization as `C_Fi = F_i / (0.5 ρ V² S)` and moment-coefficient normalization as `C_Mi = M_i / (0.5 ρ V² S c_CE)`, with `c_CE = 0.250 m` (Eqs. 26–27). Reported optimum sheet angle is 90° in the light-wind case and 80° for strong-wind dead-downwind; tight kicker gives the largest drive force. | Direct sail-force evidence, but downwind only. The force/moment normalization and qualitative trim result are usable. The paper says the full test matrix is very large; full numeric coefficient data have not been transcribed, and chart readings are not being treated as coefficient targets. No bulk dataset retrieval was done. |
| Magnander, [Aerodynamic Analysis of the Bi-Radial MKII Sail for the Olympic Sailing Class Dinghy ILCA 7](https://odr.chalmers.se/items/edcb1ca9-d053-486d-b3e9-a9dafd7d6d6b), Master's thesis (2022) | Describes the sail coefficient work and its use with full-scale 2014 ILCA towing-tank hydrodynamic coefficients in a VPP. Test focus is downwind in flat water. The repository lists a 29.98 MB thesis PDF. | Candidate for a downwind VPP and for interpreting the underlying sail data. Numeric polar values, wind angles, crew mass, and trim assumptions have not been extracted or verified. A 29.98 MB source download is deferred pending user direction. |
| Pennanen, Levin et al., [Numerical prediction of the best heel and trim of a Laser dinghy](https://research.chalmers.se/publication/234579), *Procedia Engineering* 147 (2016), pp. 336–341, DOI 10.1016/j.proeng.2016.06.304 | Upwind, flat-water IVPP/CFD study (full text, pp. 338–340, Figs. 3–6). At 2 knots, optimum heel is about 11° and trim was fixed at about 2° bow-down. At 3–5 knots, simulations cover three heel angles between 0° and 10–16° and four or five trim angles; optimum trim moves from about 0.25° bow-up at zero heel to about 0.5° bow-down at 16° heel. At 5 knots, minimum required wind occurs near 8° heel. At 3 knots, the required-wind difference between 0° and 8° heel is 0.035 knots (0.7%); at 4 knots, it is 0.11 knots (1.6%). The study says it covers upwind cases to approximately 11 knots true wind. | These are optimum-trim/heel results at selected boat speeds, not a speed polar. They are useful qualitative/narrow-point targets for checking trim and heel trends, not for claiming general ILCA 7 performance. Flat water, Laser rig/model details, and unreported crew-mass compatibility limit transfer. Figure/table digitization is not required for this initial narrow target. |
| [Performance prediction for sailing dinghies](https://www.sciencedirect.com/science/article/pii/S0029801817300938), *Ocean Engineering* (2017) | A Laser-dinghy VPP is checked against measured upwind data; the paper studies crew weight relative to an 80 kg baseline sailor and reports that crew weight affects race performance materially. | Supports 80 kg as an explicit reference crew mass in a related Laser VPP, but does not by itself establish compatibility with the 2022 ILCA 7 VPP or the force dataset. It is a separate candidate validation source, not a merged target. |

### Calibration rules from this review

- No numeric target should be entered for untranscribed coefficient or VPP tables.
- Keep downwind flat-water results separate from upwind results and from the simulator's wave conditions.
- Treat the 2016 heel/trim results as local upwind trend checks only; they do not define a speed polar or complete trim map.
- For every future target, record apparent/true wind speed, wind-angle convention, boat speed, heel, trim, water state, crew/rig assumptions, units, and source location.
- Treat any interpolation or extrapolation outside a source's measured cases as a model estimate, not as reference data.

## Remaining Phase 1 research

Roadmap note (2026-10-27): research findings below are retained; work ownership
is updated for the revised roadmap.
Objective-data extraction, compatibility review and calibration gaps now hand
off to [Phase 11](11-calibration-and-realism-validation.md), including rows
originally assigned to Phase 8. Missing numeric targets do not block the
engineering milestones in Phases 8–10; estimates remain documented.

| Gap | Recommended next action | Owner |
| --- | --- | --- |
| Exact 2023 coefficient values and full test grid | The paper describes the data matrix as very large. If a particular implementation needs exact samples, warn the user before any bulk retrieval and first check whether a small subset suffices. | Phase 1 / Phase 3 |
| 2022 downwind numeric VPP output and crew/rig assumptions | Numeric results remain unverified. If exact values become necessary, warn the user before retrieving the 29.98 MB thesis and inspect only relevant tables/figures. | Phase 1 / Phase 3 |
| 2016 upwind optimum heel/trim results | The textual results are recorded above. If finer calibration is later justified, digitize only the relevant figures and retain the approximate reading uncertainty. | Phase 1 / Phase 3 |
| 2016 crew-mass and rig compatibility | Verify the modeled Laser displacement, rig, and crew assumptions against the ILCA 7 baseline before transferring any result. | Phase 1 |
| Equipped-boat mass interpretation | Check the handbook statement against current class measurement rules and the exact VPP model's included items. | Phase 1 |
| Component masses and centers of mass | Find measured values in suitable sources or keep documented estimates; no large-scale data retrieval is justified yet. | Phase 1 / Phase 3 |
| Wave height, wavelength, and physical ranges | Phase 4 selects documented design estimates below; validate practice conditions during calibration. | Phase 11 |
| Upwind/reaching speed and pointing targets | Locate a compatible measured or published source; avoid filling gaps from generic dinghy performance figures. | Phase 11 |

## Source review boundary

This pass used source landing pages, abstracts, handbook text, indexed article text, and the browser-extracted text of the six-page 2016 open-access paper. It did not download the 29.98 MB thesis, the 2023 coefficient dataset, or other bulk data. No complex retrieval workaround was attempted. Reviewed 2026-10-22.

## Phase 4 environment estimates (2026-10-22)

These are implementation choices, separate from the source-backed boat data.

| Quantity | Initial value | Rationale / limitation |
| --- | ---: | --- |
| Calm spectral significant height | 0 m | Exact flat/stationary comparison baseline. |
| Gentle spectral significant height | 0.12 m | Small practice waves; provisional default. |
| Moderate spectral significant height | 0.30 m | 2.5 times gentle amplitude/flow for controlled comparisons, not a named weather category. |
| Wavelength support / spectral peak | 3–12 m / 6 m | Brackets hull length for spatial immersion and motion response; avoids very short stiff ripples. |
| Directional spread | Mean ±25° | Three directional bins retain a dominant bearing and allow transverse response. |

The spectrum is normalized with `Hs = 4 sqrt(Σa²/2)`; Hs is not a maximum
wave height. The Gaussian energy shape in log wavelength, numeric ranges and
spreading weights are **unmeasured estimates**. Wave strength is independent
of wind speed, and initial current remains zero.

The implementation uses the deep-water linear dispersion/orbital-flow basis
from Krogstad & Arntsen, NTNU,
[Linear Wave Theory, Part A](https://oivarn.folk.ntnu.no/hercules_ntnu/LWTcourse/lwt_new_2000_Part_A.pdf),
sections 5–8 (2000), reviewed through browser-extracted text. That source
supports the mathematical basis, not the chosen preset values. No bulk dataset
or VPP thesis was retrieved. See [the environment model](../docs/wind-and-waves.md)
for equations, seeded realization, force coupling and omitted physics.
