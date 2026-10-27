namespace SailingSimulator.Core;

/// <summary>Single-owner fixed-step RK4 solver with atomic completed-state publication.</summary>
public sealed class Simulation
{
    public const double StepSeconds = 1.0 / 60;
    private readonly IForceModel forceModel;
    private readonly MassProperties mass;
    private readonly IMassModel? massModel;
    private readonly SnapshotMailbox<SimulationState> output;
    public SimulationState Latest => output.Read();

    public Simulation(RigidBodyState initial, MassProperties mass, IForceModel forceModel, SimulationInput initialInput)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.forceModel = forceModel ?? throw new ArgumentNullException(nameof(forceModel));
        ArgumentNullException.ThrowIfNull(initialInput);
        var boat = initial.ValidatedAndNormalized();
        var rig = forceModel is IRigDynamicsModel rigModel ? rigModel.InitialRig : default;
        ValidateRig(rig);
        output = new(new SimulationState(0, 0, boat, initialInput)
        { Mass = mass, Rig = rig, Diagnostics = Diagnostics(0, boat, mass, initialInput, rig),
            HunterRig = HunterDiagnostics(0, boat, mass, initialInput, rig),
            HunterGeometry = HunterGeometry(initialInput.Controls) });
    }

    public Simulation(RigidBodyState initial, IMassModel massModel, IForceModel forceModel, SimulationInput initialInput)
        : this(initial, (massModel ?? throw new ArgumentNullException(nameof(massModel))).Evaluate(
            (initialInput ?? throw new ArgumentNullException(nameof(initialInput))).Controls), forceModel, initialInput)
        => this.massModel = massModel;

    /// <summary>Advance exactly 1/60 s. The caller owns wall-clock scheduling; never call concurrently.</summary>
    public SimulationState Step(SimulationInput input, StepProfiler? profiler = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var before = Latest;
        var index = checked(before.StepIndex + 1);
        var t = before.TimeSeconds;
        var y = new IntegratedState(before.Boat, before.Rig);
        MassProperties stepMass;
        try
        {
            using var measurement = profiler?.Measure(SimulationSubsystem.Mass);
            stepMass = massModel is null ? mass : input.Controls == before.Input.Controls
                ? before.Mass! : massModel.Evaluate(input.Controls);
            if (stepMass.MassKg != mass.MassKg) throw new ArgumentException("Internal mass motion cannot change total mass.");
            // Quasi-static crew/board repositioning preserves COM and total angular momentum.
            if (stepMass.InertiaBodyKgM2 != before.Mass!.InertiaBodyKgM2)
                y = y with { Boat = y.Boat with { AngularVelocityBodyRadPerSec = stepMass.InertiaBodyKgM2.Solve(
                    before.Mass.InertiaBodyKgM2.Multiply(y.Boat.AngularVelocityBodyRadPerSec)) } };
        }
        catch (Exception exception) when (exception is ArgumentException or ArithmeticException)
        { throw new SimulationStepException(index, "mass", exception); }
        StateDerivative k1, k2, k3, k4;
        using (profiler?.Measure(SimulationSubsystem.Rk4))
        {
            k1 = Derivative(y, t, input, stepMass, index, "k1", profiler);
            k2 = Derivative(Advance(y, k1, StepSeconds / 2), t + StepSeconds / 2, input, stepMass, index, "k2", profiler);
            k3 = Derivative(Advance(y, k2, StepSeconds / 2), t + StepSeconds / 2, input, stepMass, index, "k3", profiler);
            k4 = Derivative(Advance(y, k3, StepSeconds), t + StepSeconds, input, stepMass, index, "k4", profiler);
        }
        RigidBodyState completed;
        RigState completedRig;
        BoatDiagnostics? diagnostics;
        HunterRigEvaluation? hunterRig;
        HunterBoatGeometry? hunterGeometry;
        try
        {
            var result = Advance(y, (k1 + k2 * 2 + k3 * 2 + k4) * (1.0 / 6), StepSeconds);
            completed = result.Boat.ValidatedAndNormalized();
            completedRig = result.Rig;
            ValidateRig(completedRig);
            using var measurement = profiler?.Measure(SimulationSubsystem.Diagnostics);
            diagnostics = Diagnostics(index * StepSeconds, completed, stepMass, input, completedRig, profiler);
            hunterRig = HunterDiagnostics(index * StepSeconds, completed, stepMass, input, completedRig, profiler);
            hunterGeometry = HunterGeometry(input.Controls);
        }
        catch (Exception exception) when (exception is ArgumentException or ArithmeticException)
        {
            throw new SimulationStepException(index, "completion", exception);
        }
        var snapshot = new SimulationState(index, index * StepSeconds, completed, input)
        { Mass = stepMass, Rig = completedRig, Diagnostics = diagnostics, HunterRig = hunterRig, HunterGeometry = hunterGeometry };
        output.Publish(snapshot);
        return snapshot;
    }

    private BoatDiagnostics? Diagnostics(double time, RigidBodyState boat, MassProperties currentMass, SimulationInput input, RigState rig,
        StepProfiler? profiler = null) =>
        forceModel is IBoatDiagnosticsModel model ? model.Inspect(new(time, boat, currentMass, input, rig, profiler)) : null;

    private HunterRigEvaluation? HunterDiagnostics(double time, RigidBodyState boat, MassProperties currentMass, SimulationInput input, RigState rig,
        StepProfiler? profiler = null) =>
        forceModel is IHunterRigInspectionModel model ? model.InspectHunterRig(new(time, boat, currentMass, input, rig, profiler)) : null;

    private HunterBoatGeometry? HunterGeometry(SailingControls controls) =>
        forceModel is IHunterRigInspectionModel model ? model.InspectHunterGeometry(controls) : null;

    private StateDerivative Derivative(IntegratedState raw, double time, SimulationInput input, MassProperties currentMass, long index, string stage,
        StepProfiler? profiler)
    {
        try
        {
            var boat = raw.Boat.ValidatedAndNormalized();
            ValidateRig(raw.Rig);
            var evaluation = new ForceEvaluation(time, boat, currentMass, input, raw.Rig, profiler);
            var dynamics = forceModel is IRigDynamicsModel rigModel ? rigModel.EvaluateDynamics(in evaluation)
                : (forceModel.Evaluate(in evaluation), default(RigDerivative));
            var (forces, rigRate) = dynamics;
            if (!forces.IsFinite) throw new ArithmeticException("Force model returned a non-finite force or moment.");
            var omega = boat.AngularVelocityBodyRadPerSec;
            var inertia = currentMass.InertiaBodyKgM2;
            var angularMomentumBody = inertia.Multiply(omega);
            var netMoment = forces.MomentBodyNm - Vector3d.Cross(omega, angularMomentumBody);
            var angularAccelerationBody = inertia.Solve(netMoment);
            var orientationRate = (raw.Boat.OrientationBodyToWorld * new Quaterniond(omega.X, omega.Y, omega.Z, 0)) * 0.5;
            var result = new StateDerivative(boat.LinearVelocityWorldMps, orientationRate, forces.ForceWorldN / currentMass.MassKg, angularAccelerationBody,
                rigRate.AngleRate, rigRate.AngularAcceleration, rigRate.JibAngleRate, rigRate.JibAngularAcceleration);
            if (!result.IsFinite) throw new ArithmeticException("Non-finite state derivative.");
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or ArithmeticException)
        {
            throw new SimulationStepException(index, stage, exception);
        }
    }

    private static void ValidateRig(RigState rig)
    {
        if (!double.IsFinite(rig.BoomAngleRad) || !double.IsFinite(rig.BoomRateRadPerSec) ||
            !double.IsFinite(rig.JibAngleRad) || !double.IsFinite(rig.JibRateRadPerSec))
            throw new ArithmeticException("Non-finite rig state.");
    }

    private readonly record struct IntegratedState(RigidBodyState Boat, RigState Rig);

    private static IntegratedState Advance(IntegratedState y, StateDerivative k, double dt) => new(new(
        y.Boat.PositionWorldM + k.Position * dt, y.Boat.OrientationBodyToWorld + k.Orientation * dt,
        y.Boat.LinearVelocityWorldMps + k.Velocity * dt, y.Boat.AngularVelocityBodyRadPerSec + k.AngularVelocity * dt),
        new(y.Rig.BoomAngleRad + k.BoomAngle * dt, y.Rig.BoomRateRadPerSec + k.BoomRate * dt,
            y.Rig.JibAngleRad + k.JibAngle * dt, y.Rig.JibRateRadPerSec + k.JibRate * dt));

    private readonly record struct StateDerivative(Vector3d Position, Quaterniond Orientation, Vector3d Velocity, Vector3d AngularVelocity,
        double BoomAngle, double BoomRate, double JibAngle, double JibRate)
    {
        public bool IsFinite => Position.IsFinite && Orientation.IsFinite && Velocity.IsFinite && AngularVelocity.IsFinite && double.IsFinite(BoomAngle) && double.IsFinite(BoomRate) && double.IsFinite(JibAngle) && double.IsFinite(JibRate);
        public static StateDerivative operator +(StateDerivative a, StateDerivative b) =>
            new(a.Position + b.Position, a.Orientation + b.Orientation, a.Velocity + b.Velocity, a.AngularVelocity + b.AngularVelocity,
                a.BoomAngle + b.BoomAngle, a.BoomRate + b.BoomRate, a.JibAngle + b.JibAngle, a.JibRate + b.JibRate);
        public static StateDerivative operator *(StateDerivative a, double s) => new(a.Position * s, a.Orientation * s, a.Velocity * s, a.AngularVelocity * s,
            a.BoomAngle * s, a.BoomRate * s, a.JibAngle * s, a.JibRate * s);
    }
}

/// <summary>Invalid integration result; the last valid snapshot remains published.</summary>
public sealed class SimulationStepException : Exception
{
    public long AttemptedStepIndex { get; }
    public string Stage { get; }
    public SimulationStepException(long attemptedStepIndex, string stage, Exception innerException)
        : base($"Simulation step {attemptedStepIndex} failed at {stage}: {innerException.Message}", innerException)
    {
        AttemptedStepIndex = attemptedStepIndex;
        Stage = stage;
    }
}
