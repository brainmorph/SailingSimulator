# Hunter 23.5 reference research: preliminary register

Research date: 2026-10-27. Phase 8 remains open; Phase 9 has started at the
user's direction. This is not an implementation-ready model contract or completed Phase 9.1.
The selected configuration is the H23.5 with full water ballast, full main and
jib, adjustable centerboard and an 80 kg skipper. Other Hunter models are not
substitutes. Measurements below are reference leads, not calibrated parameters.

## Configuration and source register

| Source | Findings and intended use | Limits |
| --- | --- | --- |
| [Hunter Water Ballast Series owner's manual, H23.5 appendix](https://manualzz.com/doc/6336751/hunter-marine-h19--h23.5--h26-sailboats-owner-s-manual) | Manufacturer manual identifies H23.5 sail plan H23-A-2609, deck hardware layout, and centerboard control system H23-A-2616. Its sailing instructions describe a full, sealed ballast tank, lowered centerboard, main and jib. | The web transcription is badly corrupted, combines three model appendices, and loses drawings. Inspect the original H23.5 drawings before extracting measurements or positions; do not use garbled OCR as numeric evidence. |
| [HunterOwners H23.5 specification page](https://hunter.sailboatowners.com/boats.php?mid=8) | Secondary model-specific listing: LOA 23 ft 8 in, LWL 21 ft 5 in, beam 8 ft 4 in, draft 18 in/5 ft 6 in, displacement 3,000 lb, ballast 1,000 lb, reported sail area 236 ft², I 25 ft 6 in, J 8 ft 6 in, P 25 ft 8 in, E 10 ft. | Editable listing, not an original drawing. Displacement loading, equipment and crew assumptions are unstated. Sail area is not a separate main/jib measurement. |
| [Sailboatdata H23.5](https://sailboatdata.com/sailboat/hunter-235/) | Independent-looking secondary compilation reports essentially the same full-ballast figures. 3,000 lb ≈ 1,360.8 kg and 1,000 lb ≈ 453.6 kg; subtraction gives 907.2 kg as a provisional non-ballast remainder. | Shared underlying sources are possible. The remainder is arithmetic, not a component mass measurement; do not add ballast again to the 3,000 lb full-ballast displacement. Whether equipment and crew are included remains unresolved. |
| [Good Old Boat H23.5 sail data](https://goodoldboat.com/saildata/boat/hunter-235/) | Illustrates candidate replacement-sail dimensions. | Its asterisked sail measurements and areas are calculated from rig dimensions. They do not establish actual factory main or jib outline or area. Its prose quotes 1,100 lb ballast, conflicting with the 1,000 lb secondary listings; do not reconcile by assumption. |
| [PHRF Lake Erie master class list, 2020](https://phrf-le.org/wp-content/uploads/2025/03/Base-Handicaps-8.1.20-Rating-New.pdf) | Lists HUNTER 23.5 CB and class handicap 249, plus rig and hull fields. | Handicap is a broad rating cross-check, not a speed polar; individual ratings vary. Its I = 25.0 ft and P = 25.8 ft differ from the secondary listing, and its source/configuration for those fields is unclear. Do not silently combine them. |

A plausible working accounting hypothesis is 1,360.8 kg for the full-ballast
boat plus a separate 80 kg skipper, but this is **not yet an auditable mass
budget**. Crew/equipment inclusion, exact tank mass and location, board mass,
component centers and inertia remain unverified. No numerical component
allocations or sail attachment coordinates are adopted in this record.

## Initial performance search

Searches for Hunter 23.5 speed tables, measured trials, polar/VPP and PHRF
records located the following leads; no attributable, condition-complete
numerical polar was identified in this search.

- [Practical Sailor's Newport test](https://www.practical-sailor.com/sailboat-reviews/hunter-23-5/)
  is a firsthand qualitative test. It describes light-air handling and a
  15–20-knot case with one reef in the main. Wind-angle, water speed,
  crew/loading, sea state and instrument details are insufficient for a polar;
  the reefed case is incompatible with the selected full-sail configuration.
- [ORC certificate description](https://orc.org/organization/monohulls/orc-club-certificate)
  and [Sailor Services](https://orc.org/sailors/sailor-services) describe a
  route to VPP predictions. A specific H23.5 certificate, configuration and
  polar have not been verified. Predictions, if found, must be labelled as such.
- The PHRF Lake Erie class listing above is a rating, without a wind/angle speed
  table. It is not a measured speed target.
- An [owner forum discussion](https://forums.sailboatowners.com/threads/does-anybody-have-a-speed-table-for-the-hunter-23-5.1249942027/)
  offers anecdotal speed claims without complete conditions or instrument
  provenance. It is not a numerical calibration target.

For any future target retain true/apparent wind, speed through water/over
ground, current, waves, crew and loading, board/ballast, sail inventory and
reefing, source location and uncertainty. Qualitative reports do not prove
performance; calculated hull speed is not a polar. Phase 11 owns objective
comparisons and calibration.

## Remaining work before model implementation

An additional search found [SEA Sailmakers' H23.5 listing](https://seasailmakers.com/sailboats/hunter-23-5),
which quotes a 13.36 m² replacement mainsail and 11.93 m² triangular area.
Another [replacement sail listing](https://southeastasiasails.com/hunter-sails/full-batten-cross-cut-hunter-235-mainsail)
quotes approximately 14.31 m² and says its dimensions are based on a database.
Neither establishes the factory sail outline or actual installed jib; the differing
areas illustrate why the calculated triangles cannot be treated as measured sails.
The manufacturer archive listing timed out during this search and the original
H23.5 drawings remain unreviewed.

On 2026-10-27 a direct manual PDF link was identified in an
[owner discussion](https://forums.sailboatowners.com/threads/how-to-use-hunter-23-5-centerboard-uphaul-blocks.154049/),
but the [PDF](https://sbo.sailboatowners.com/downloads/Hunter_23.5_50853939.pdf)
timed out when fetched. The indexed manual text does identify the H23.5 sail
plan and rigging list, but its drawing text remains too corrupted to extract
dimensions reliably. No original drawing measurements are claimed from this attempt.

Inspect original H23.5 drawings, confirm boat displacement interpretation,
main and jib areas separately, sail corners and sheet leads, centerboard and
rudder geometry, ballast tank and cockpit positions. Establish a traceable
mass and inertia budget with explicit estimates, and record the selected
configuration and control/geometry contracts. The existing archive lists a
2,665 KB manual, but this pass did not retrieve the original PDF or review its
drawings. Phase 8's handling, visual and desktop performance gates remain open.
