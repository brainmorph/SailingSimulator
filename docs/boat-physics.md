# Initial ILCA 7 physics

Phase 3 is an **uncalibrated reduced-order model**, in flat fresh water with
uniform steady wind. It is a starting point for wave integration and later
calibration, not a prediction of measured ILCA speed or handling. No published
coefficient table or VPP polar has been substituted with invented targets.

## Run and inspect

```sh
dotnet run --project tests/BoatPhysicsCheck
dotnet run --project tools/SimulationHarness -- --boat --steps 600
```

The harness compares two identical complete trajectories and emits the final
state, mass properties, boom state, six force/moment contributions, and six
sail sections as JSON. It runs offline; it does not schedule or benchmark
60 Hz wall-clock execution. Without `--boat` it retains the synthetic Phase 2
coasting scenario.

```csharp
var input = new SimulationInput(new SailingControls(mainsheet: 0.6),
    PresentationConversions.EnvironmentFromSettings(wavePreset: WavePreset.Calm));
var model = new IlcaBoatModel();
var simulation = new Simulation(model.CreateInitialState(input),
    new IlcaMassModel(), model, input);
var snapshot = simulation.Step(input);
```

`CreateInitialState` solves upright heave equilibrium by bisection; roll and
pitch are free to settle. Its position is the **total centre of mass**, as in
the foundation. It uses the ILCA mass model and is not a generic initializer
for arbitrary bodies. Wind is active even though the initialization balances
only buoyancy and gravity. Wave settings are retained but ignored in Phase 3.

## Mass, datum and internal motion

Hull-datum X=0 is the middle of the nominal waterline, Y=0 the centreplane,
Z=0 an arbitrary waterline reference. Coordinates below use nautical body
axes, metres and kilograms. Hull/rig inertia estimates are intrinsic about
each contributor's own centre, in body axes.

| Contributor | Mass kg | Centre (X,Y,Z) m | Intrinsic (Ixx,Iyy,Izz) kg m² |
| --- | ---: | --- | --- |
| Hull | 42 | (0,0,0.03) | (7,48,53) |
| Rig, including sail | 9 | (0.35,0,-2.1) | (24,27,4) |
| Centerboard | 2.5 | (0.05,0,0.35−0.65(1−lowering)) | (0.18,0.2,0.025) |
| Rudder assembly | 1.5 | (−2,0,0.28) | (0.04,0.05,0.02) |
| Fittings | 3 | (−0.2,0,−0.05) | (0.1,1,1) |
| Sailor, one point | 80 | (0,0.85h,−0.32−0.12\|h\|) | (0,0,0) |

**Every component allocation, centre and intrinsic inertia is an estimate.**
The first five rows total exactly the plan's provisional 58 kg equipped
budget; the sailor is added once, yielding 138 kg. No additional rig or foil
mass is added outside that budget. The rig mass is fixed while the reduced-order
boom moves; its mass distribution does not follow individual sail sections.

The class-rule check on 2026-10-22 did **not** resolve whether the handbook's
58 kg includes all removable equipment. The
[current ILCA rules](https://www.sailing.org/wp-content/uploads/2020/02/ILCA_CR_10-August-2026.pdf),
effective 10 August 2026, direct construction/dimensions to the ILCA Build
Manual (D.2.2, E.2.2 and F.2.2); their extracted text does not define a 58 kg
equipped mass budget. Keep the plan's interpretation provisional until measured
component weights or compatible study assumptions establish it. No large
dataset or thesis download was undertaken.

`IlcaMassModel` returns mass-weighted centre and a full symmetric inertia
tensor using the parallel-axis theorem, including products of inertia.
`InertiaTensor.Solve` uses Cholesky factorization and rejects non-positive
definite tensors. Euler dynamics use `I⁻¹(M − ω × Iω)`.

Positive hiking moves the sailor to starboard; negative moves to port. Hiking
therefore rights heel to the opposite side through the buoyancy lever about
the shifted combined centre, rather than an extra artificial torque. Gravity
acts at the combined centre and has zero moment about it.

`IMassModel` is a pure function of controls, sampled once before RK4. On control
changes the solver preserves total COM position/velocity and angular momentum,
solving `Inew ωnew = Iold ωold`. The hull datum consequently moves relative to
the combined COM. This is a quasi-static internal repositioning approximation:
crew transit velocity, internal angular momentum and articulated-body reaction
forces are omitted; the sailor can do work. Controls should change gradually
in the eventual host. A single abrupt input changes mass geometry immediately;
the solver does not disguise it with a force clamp or a different timestep.

## Hull and foils

Sources for 4.23 m length, 3.81 m waterline, 1.37 m beam and 7.06 m² sail area
remain in [the reference baseline](../phases/01-reference-baseline.md).
All other geometry and coefficients below are **model estimates**, not digitized
hull lines, measured towing coefficients or class measurement diagrams.

The hull has 12 longitudinal by 4 transverse quadrature columns. At midpoint X,
half-width is `0.685 sqrt(1−(2X/4.23)²)`, across a 3.81 m waterline. Column bottom
Z is `0.12−0.10(X/1.905)²−0.06(Y/halfWidth)²`, with 0.32 m sealed height.
This rounded waterplane and rocker supply initial restoring stiffness without
claiming a measured hull surface. Column waterplane areas are `ΔX ΔY`.
Each column's submerged volume produces vertical Archimedean buoyancy at its
wet centroid, using 1000 kg/m³ fresh water and gravity 9.80665 m/s². Rotated
endpoints determine the submerged fraction; transverse quadrature approximates
the continuous hull. There is no flooded-hull or capsize recovery model.

At each wet centroid, local velocity includes `ω × offset`. Drag is
`−Awet (0.5 ρ Cd vi |vi| + bi vi)` per body axis. Estimated `(CdX,CdY,CdZ)`
are `(0.012,0.08,0.80)`; linear coefficients `(bX,bY,bZ)` are `(0.4,5,30)`
N s/m³. `Awet` is column area times `min(1, submergedLength/0.05 m)`.
The smaller transverse coefficient approximates the shallow hull's side
projection relative to its waterplane; applying broadside pressure to the full
waterplane excessively suppresses yaw. The linear terms represent low-speed
dissipative resistance; no hidden global
angular damping is added. Distributed lever arms supply roll, pitch and yaw
resistance. Wave-making resistance, planing, spray and wetted-surface geometry
are not calibrated.

Centerboard and rudder are separate `FoilModel` instances. Estimated fully
lowered area/span are 0.24 m²/0.75 m and 0.10 m²/0.48 m. Their top points are
(0.05,0,0.08) and (−2.05,0,0.08) m. Board lowering scales area and span to zero;
immersion further scales area. Force acts at the wet span centroid. The rudder
remains down in this scope. Positive tiller rotates its forward chord by
−0.45 rad per normalized input, yielding positive/starboard yaw in forward flow.
There is no speed-independent steering torque.

Relative local flow resolves into chord/normal components. Foil lift uses
`Cl=1.1 sin(2α)` and drag `Cd=0.012+0.09Cl²+0.8 sin²α`; lift is perpendicular
to planar velocity and drag opposes it. These smooth provisional curves lose
lift at broadside incidence and handle reverse flow without an unbounded
linear lift slope. Spanwise drag uses coefficient 0.08. Passive foil/hull forces
are tested for nonpositive mechanical power, including rotational motion.

## Sail, trim, boom and snapshots

Six equal-height strips cover an estimated 5.2 m luff starting 0.5 m above the
hull datum, at mast X=0.65 m. Linear chord taper integrates exactly to the
referenced 7.06 m² area; force acts at 40% chord. These positions are estimates.
Air density is 1.225 kg/m³. Each section's apparent wind is true world wind
minus local hull velocity and boom motion, transformed to body axes.

All angles below are radians. Positive boom angle opens toward starboard,
around the upward body axis; it is not a positive body-Z yaw rotation.

- Mainsheet/traveler set the compliant opening limit:
  `0.08+1.35(1−mainsheet)+0.20(1−traveler)`.
- Camber is `0.045+0.085(1−outhaul)(1−0.45 cunningham)`.
- Full-height twist is
  `(0.06+0.42(1−vang)+0.10(1−cunningham)) tanh(boomAngle/0.12)`;
  each strip takes its fractional height times this twist.
- Luffing is `exp(−(sin α/0.12)²)` for chord-relative flow angle α.
  The force multiplier is `1−luffing`. At zero airflow force is exactly zero.
- Estimated aerodynamic curves are
  `Cl=(1.1+1.8 camber) sin(2α)(1−luffing)` and
  `Cd=(0.025+0.12 camber+1.35 sin²α)(1−luffing)`.
  Lift is perpendicular to planar apparent wind; drag follows it.

Boom angle and rate are additional states integrated in the same four RK4
stages as the boat. The estimated boom inertia is 10 kg m², sheet spring
1500 N m/rad outside the opening limit, and damping `18+12 vang` N m s/rad.
Aerodynamic moment about the mast drives the boom. The sheet is compliant,
so its limit can be exceeded under load. Initialization uses a small 0.1 rad
starboard offset to select one side in symmetric flow. There are no tack,
jibe, target-heading or target-speed actions. Boom/hull inertial reaction
coupling and gravity on the moving boom are omitted in this initial rig
approximation; do not infer angular-momentum conservation for the complete
boat-plus-dynamic-boom system from the rigid-body conservation tests.

`SimulationState.Rig`, `.Mass` and `.Diagnostics` accompany the completed pose
and sampled input. Diagnostics are reevaluated at the completed state, not
copied from the final RK4 intermediate stage. Gravity, buoyancy, hull drag,
centerboard, rudder and sail are independently inspectable, as are each
section's force, shape, local airflow and continuous luffing value. Section
collections are read-only and owned by the snapshot. Rendering should use
those sections for sail shape and telltales rather than calculate a second
trim model. Collection record equality is reference equality; compare section
values for deterministic replay, as the harness and regression checks do.

## Verification boundary and next work

The regression executable covers mass budgets, tensors, internal momentum,
hydrostatic restoring, passive drag power, steering signs, all controls,
shared sail/force output, luffing, dynamic boom crossing, deterministic replay,
and fixed-step bounded motion at 0, 10 and 30 knots. These are physical-contract
and numerical tests, not sailor validation or a polar calibration. Strong wind
or poor hiking can overturn the boat; capsize recovery is deferred.

Phase 4 should replace the flat-water elevation/velocity assumption at hull
and foil samples with a seeded surface evaluated at each RK4 stage's time.
Retain the still-water regression baseline. Godot controls/threading remain
Phase 5, rendering Phase 6, instrumentation Phase 7 and performance calibration
Phase 8. The unresolved mass and source-compatibility questions still apply.
