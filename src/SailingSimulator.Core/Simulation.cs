namespace SailingSimulator.Core;

/// <summary>Single-owner fixed-step RK4 solver with atomic completed-state publication.</summary>
public sealed class Simulation
{
    public const double StepSeconds = 1.0 / 60;
    private readonly IForceModel forceModel;
    private readonly MassProperties mass;
    private readonly SnapshotMailbox<SimulationState> output;
    public SimulationState Latest => output.Read();

    public Simulation(RigidBodyState initial, MassProperties mass, IForceModel forceModel, SimulationInput initialInput)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.forceModel = forceModel ?? throw new ArgumentNullException(nameof(forceModel));
        ArgumentNullException.ThrowIfNull(initialInput);
        output = new(new(0, 0, initial.ValidatedAndNormalized(), initialInput));
    }

    /// <summary>Advance exactly 1/60 s. The caller owns wall-clock scheduling; never call concurrently.</summary>
    public SimulationState Step(SimulationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var before = Latest;
        var index = checked(before.StepIndex + 1);
        var t = before.TimeSeconds;
        var y = before.Boat;
        var k1 = Derivative(y, t, input, index, "k1");
        var k2 = Derivative(Advance(y, k1, StepSeconds / 2), t + StepSeconds / 2, input, index, "k2");
        var k3 = Derivative(Advance(y, k2, StepSeconds / 2), t + StepSeconds / 2, input, index, "k3");
        var k4 = Derivative(Advance(y, k3, StepSeconds), t + StepSeconds, input, index, "k4");
        RigidBodyState completed;
        try
        {
            completed = Advance(y, (k1 + k2 * 2 + k3 * 2 + k4) * (1.0 / 6), StepSeconds).ValidatedAndNormalized();
        }
        catch (ArgumentException exception)
        {
            throw new SimulationStepException(index, "completion", exception);
        }
        var snapshot = new SimulationState(index, index * StepSeconds, completed, input);
        output.Publish(snapshot);
        return snapshot;
    }

    private StateDerivative Derivative(RigidBodyState raw, double time, SimulationInput input, long index, string stage)
    {
        try
        {
            var boat = raw.ValidatedAndNormalized();
            var evaluation = new ForceEvaluation(time, boat, mass, input);
            var forces = forceModel.Evaluate(in evaluation);
            if (!forces.IsFinite) throw new ArithmeticException("Force model returned a non-finite force or moment.");
            var omega = boat.AngularVelocityBodyRadPerSec;
            var inertia = mass.PrincipalInertiaKgM2;
            var angularMomentumBody = new Vector3d(inertia.X * omega.X, inertia.Y * omega.Y, inertia.Z * omega.Z);
            var netMoment = forces.MomentBodyNm - Vector3d.Cross(omega, angularMomentumBody);
            var angularAccelerationBody = new Vector3d(netMoment.X / inertia.X, netMoment.Y / inertia.Y, netMoment.Z / inertia.Z);
            var orientationRate = (raw.OrientationBodyToWorld * new Quaterniond(omega.X, omega.Y, omega.Z, 0)) * 0.5;
            var result = new StateDerivative(boat.LinearVelocityWorldMps, orientationRate, forces.ForceWorldN / mass.MassKg, angularAccelerationBody);
            if (!result.IsFinite) throw new ArithmeticException("Non-finite state derivative.");
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or ArithmeticException)
        {
            throw new SimulationStepException(index, stage, exception);
        }
    }

    private static RigidBodyState Advance(RigidBodyState y, StateDerivative k, double dt) => new(
        y.PositionWorldM + k.Position * dt, y.OrientationBodyToWorld + k.Orientation * dt,
        y.LinearVelocityWorldMps + k.Velocity * dt, y.AngularVelocityBodyRadPerSec + k.AngularVelocity * dt);

    private readonly record struct StateDerivative(Vector3d Position, Quaterniond Orientation, Vector3d Velocity, Vector3d AngularVelocity)
    {
        public bool IsFinite => Position.IsFinite && Orientation.IsFinite && Velocity.IsFinite && AngularVelocity.IsFinite;
        public static StateDerivative operator +(StateDerivative a, StateDerivative b) =>
            new(a.Position + b.Position, a.Orientation + b.Orientation, a.Velocity + b.Velocity, a.AngularVelocity + b.AngularVelocity);
        public static StateDerivative operator *(StateDerivative a, double s) => new(a.Position * s, a.Orientation * s, a.Velocity * s, a.AngularVelocity * s);
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
