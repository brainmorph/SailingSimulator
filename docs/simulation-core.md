# Simulation foundation

## Units and frames

The core uses double-precision SI values. `Vector3d` and `Quaterniond` avoid
single-precision accumulation during long offline runs. Body axes are +X
forward, +Y starboard, +Z down. The fixed world reference is north/east/down
(NED): +X north, +Y east, +Z down. Both frames are right-handed.

`RigidBodyState.PositionWorldM` is the centre of mass, with an arbitrary initial
world origin. `LinearVelocityWorldMps` is its world-relative velocity.
`AngularVelocityBodyRadPerSec` is body-relative angular velocity. The Hamilton
quaternion `(X,Y,Z,W)` actively rotates body vectors into world coordinates.
Identity means upright and pointing north. Composition `a * b` applies `b`
first, then `a`; the inverse of a unit orientation is its conjugate.

Godot conversion lives in `SailingSimulator.Adapters`. NED `(x,y,z)` becomes
Godot `(y,-z,-x)`: north is -Z, east +X, and down -Y. Author local boat meshes
with forward -Z, starboard +X, and up +Y. Re-expressing both local and world
bases gives quaternion `(Y,-Z,-X,W)`, equivalent to `C q C^-1`. The explicit
`GodotVector3d` and `GodotQuaterniond` boundary types are independent of Godot;
the future host will construct actual Godot values from them.

`GetAttitude` derives ZYX Euler display angles in radians: positive heel is
starboard down, pitch is bow up, and heading increases clockwise from north
in [0, 2π). Euler display angles are ambiguous at vertical pitch; integration
never uses them. Degrees and knots conversions exist only in the adapters.

## Inputs and configuration

`SailingControls` validates all eight controls at construction. Tiller and hiking
are [-1,1]; the other six are [0,1]. Zero means eased/released or raised; one
means tightened or lowered. Defaults are zero except the fully lowered
centerboard. Phase 3 owns physical tiller and hiking sign mapping.

`EnvironmentFromSettings` accepts 0–30 knots and compass from-angles in [0,360]
at the boundary. It converts wind into a horizontal NED velocity pointing
opposite the compass bearing. A north wind moves south, so at 10 knots it is
`(-5.144444444..., 0, 0)` m/s. One knot is exactly 1852/3600 m/s.

The immutable core `EnvironmentConfig` contains that velocity, wave preset,
direction mode, horizontal unit wave propagation vector, and integer seed.
Directions are propagation vectors rather than compass degrees. Aligned waves
retain the wind's requested direction even at zero wind speed. Independent
waves require their own from-angle; aligned mode rejects an independent angle.
The default settings are 10 knots from north, gentle waves aligned with wind,
and seed 1. Callers can choose a different seed for varied sessions. No wave
surface, wave physical ranges, current, or implicit wind force exists yet.

`SimulationInput` packages immutable controls and configuration together.
`SnapshotMailbox<SimulationInput>` atomically exchanges the whole reference;
one `Read()` supplies one step. All RK4 stages receive that same reference.
The mailbox itself does not advance the simulation.

## Stepping and forces

```csharp
var input = new SimulationInput(new SailingControls(),
    PresentationConversions.EnvironmentFromSettings());
var simulation = new Simulation(RigidBodyState.AtRest,
    new MassProperties(1, new Vector3d(1, 1, 1)), // synthetic test body
    new CompositeForceModel(), input);
var inputs = new SnapshotMailbox<SimulationInput>(input);
var next = simulation.Step(inputs.Read());
```

`Step` always advances 1/60 s. It has no variable-dt overload, substeps, sleeps,
catch-up policy, or timestep reduction. The host owns the 60 Hz wall-clock
schedule, deadline diagnostics, lifecycle, and stop behavior in Phases 5 and 7.
No cadence claim follows from this offline API. One owner must call `Step`;
concurrent calls to `Step` are unsupported.

An `IForceModel` evaluates `(time, boat state, mass, sampled input)` four times
per step, at RK4 times t, t+h/2, t+h/2, and t+h. Models must be deterministic
functions of those inputs and immutable configuration. Do not mutate subsystem
state, consume sequential random numbers, touch Godot nodes, or cache a force
from the previous stage. Seeded environments should evaluate their realization
at the supplied stage time. `CompositeForceModel` sums independent sources and
copies its source list on construction.

`ForceMoment` contains force in world newtons and moment in body newton-metres
about the centre of mass. `AtBodyPoint` converts body force to world force and
computes moment as `offsetBody × forceBody`. It takes a body-relative offset
from the current centre of mass, not an absolute hull position. No gravity,
drag, buoyancy, wind, or damping is added implicitly.

The foundation uses fixed mass and positive diagonal inertia about the centre
of mass, with principal axes coincident with body axes. Its equations are:

```text
positionDotWorld = velocityWorld
velocityDotWorld = forceWorld / mass
omegaDotBody = I^-1 (momentBody - omegaBody × (I omegaBody))
quaternionDot = 0.5 quaternionBodyToWorld * (omegaBody, 0)
```

All four state components are integrated together with classical RK4. Stage
frame transforms use a normalized orientation; quaternion differentiation
uses the raw RK4 stage quaternion. Normalize only the completed orientation
and orientation copies passed to force evaluation. This retains the RK4
polynomial stages while keeping rotation operations valid.

Phase 3 must extend mass properties for separate contributors and moving sailor
position, including non-diagonal/variable inertia and centre-of-mass effects as
needed. The foundation's synthetic test masses are not ILCA estimates. It
does not claim to implement internal mass transfer or crew dynamics.

## Snapshots, errors, and numerical limits

`Simulation.Latest` is an atomic read of the last immutable completed snapshot.
It includes step index, time, boat state, and the exact input used. Time is
`StepIndex / 60.0`, computed from the integer index rather than accumulated.
Index/time zero describes initialization. A render thread may read `Latest`
without accessing a partially integrated body. No interpolation is applied.

Construction rejects invalid controls, mass, configuration, non-finite position
or velocity, and zero/non-finite quaternions. Finite initial orientations are
normalized. Quaternion norms below 1e-12 or with overflowing squared norm are
rejected. Every stage validates its state, force/moment, and derivative; the
completed state is checked before publication.

Non-finite stages and arithmetic/argument failures in force evaluation throw
`SimulationStepException` with attempted step, RK4 stage (`k1`–`k4` or
`completion`), and the original exception. Other model exceptions propagate.
A failed step does not advance the clock or replace `Latest`. A host should
surface the failure and stop stepping for diagnosis. Force models must be pure
because the solver cannot roll back model side effects. Null inputs and bad
constructor arguments fail directly with argument exceptions.

No force clipping, damping, speed limit, or silent reset hides instability.
Finite-but-inaccurate motion can still result from stiff or extreme models;
Phase 3 must check its operating range at the fixed timestep. Deterministic
replay means exact equal snapshots on the same runtime/platform with identical
inputs and pure models. Cross-platform floating-point bit identity is not
promised. Each successful step allocates one immutable output snapshot;
allocation and performance instrumentation belong to Phase 7.

## Verification

`tests/SimulationCoreCheck` checks analytic coasting, force/torque acceleration,
exponential velocity decay, RK4 times/input sampling, long rotation, torque-free
asymmetric inertia conservation, deterministic replay, force lever arms,
coordinate/quaternion conversions, compass settings, validation, failed-step
atomicity, and concurrent input/output publication. The executable exits
nonzero on any failure. Keep the Phase 1 calibration fixture check as well.
