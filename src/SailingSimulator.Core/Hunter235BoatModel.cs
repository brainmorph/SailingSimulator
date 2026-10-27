namespace SailingSimulator.Core;

/// <summary>Experimental Hunter force composition; not yet a player-ready or validated boat.</summary>
public sealed class Hunter235BoatModel : IRigDynamicsModel, IHunterRigInspectionModel
{
    private readonly bool experimentalRigMomentum;
    public GravityModel Gravity { get; } = new();
    public Hunter235HullModel Hull { get; } = new();
    public Hunter235Foils Foils { get; } = new();
    public Hunter235RigModel Rig { get; }
    public Hunter235BoatModel(Hunter235RigModel? rig = null, bool experimentalRigMomentum = false)
    {
        Rig = rig ?? new();
        this.experimentalRigMomentum = experimentalRigMomentum;
    }
    public RigState InitialRig => Rig.InitialRig;

    public ForceMoment Evaluate(in ForceEvaluation evaluation) => EvaluateDynamics(in evaluation).Forces;
    public HunterRigEvaluation InspectHunterRig(in ForceEvaluation evaluation)
    {
        var rig = Rig.Inspect(in evaluation);
        if (!experimentalRigMomentum) return rig;
        var forces = ExternalForces(in evaluation, rig.Total);
        var (_, derivative) = CoupleRig(in evaluation, forces, rig.Derivative);
        return rig with { Derivative = derivative, ExperimentalRigMomentum = true };
    }
    public HunterBoatGeometry InspectHunterGeometry(SailingControls controls) => Hunter235Geometry.ForControls(controls);

    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation evaluation)
    {
        var sail = Rig.EvaluateDynamics(in evaluation);
        var forces = ExternalForces(in evaluation, sail.Forces);
        return experimentalRigMomentum ? CoupleRig(in evaluation, forces, sail.Rig) : (forces, sail.Rig);
    }

    private ForceMoment ExternalForces(in ForceEvaluation evaluation, ForceMoment sail) =>
        Gravity.Evaluate(in evaluation) + Hull.Evaluate(in evaluation) +
        Foils.Centerboard.Evaluate(in evaluation) + Foils.Rudder.Evaluate(in evaluation) + sail;

    // Reduced-order, fixed-axis trial. Scalar rig inertias are included in the body
    // inertia when the rig is stationary relative to the hull.
    /// <summary>Trial fixed-axis rig coupling for engineering checks; not a validated articulated-body model.</summary>
    public static (ForceMoment Forces, RigDerivative Rig) CoupleRig(in ForceEvaluation e, ForceMoment external, RigDerivative rig)
    {
        const double boomInertia = Hunter235RigModel.BoomInertiaKgM2, jibInertia = Hunter235RigModel.JibInertiaKgM2;
        var boomAxis = -Vector3d.UnitZ;
        var forestay = new Vector3d(-2.5908, 0, -7.7724);
        var jibAxis = forestay / forestay.Length;
        var omega = e.Boat.AngularVelocityBodyRadPerSec;
        var inertia = e.Mass.InertiaBodyKgM2;
        var relativeMomentum = boomAxis * (boomInertia * e.Rig.BoomRateRadPerSec) +
            jibAxis * (jibInertia * e.Rig.JibRateRadPerSec);
        var baseAcceleration = inertia.Solve(external.MomentBodyNm -
            Vector3d.Cross(omega, inertia.Multiply(omega) + relativeMomentum));
        var invBoom = inertia.Solve(boomAxis);
        var invJib = inertia.Solve(jibAxis);
        var a = 1 - boomInertia * Vector3d.Dot(boomAxis, invBoom);
        var b = -jibInertia * Vector3d.Dot(boomAxis, invJib);
        var c = -boomInertia * Vector3d.Dot(jibAxis, invBoom);
        var d = 1 - jibInertia * Vector3d.Dot(jibAxis, invJib);
        var determinant = a * d - b * c;
        if (!double.IsFinite(determinant) || a <= 0 || d <= 0 || determinant <= 1e-10)
            throw new ArithmeticException("Invalid experimental rig inertia coupling.");
        var x = rig.AngularAcceleration - Vector3d.Dot(boomAxis, baseAcceleration);
        var y = rig.JibAngularAcceleration - Vector3d.Dot(jibAxis, baseAcceleration);
        var boomAccel = (x * d - b * y) / determinant;
        var jibAccel = (a * y - c * x) / determinant;
        var reaction = -boomAxis * (boomInertia * boomAccel) - jibAxis * (jibInertia * jibAccel) -
            Vector3d.Cross(omega, relativeMomentum);
        return (external with { MomentBodyNm = external.MomentBodyNm + reaction },
            rig with { AngularAcceleration = boomAccel, JibAngularAcceleration = jibAccel });
    }
}

/// <summary>Creates an experimental Hunter simulation with provisional sail diagnostics.</summary>
public sealed class Hunter235BoatDefinition : IBoatDefinition
{
    public string Id => "hunter-235";
    public Simulation CreateSimulation(SimulationInput input, double headingRad = 0)
    {
        ArgumentNullException.ThrowIfNull(input);
        var model = new Hunter235BoatModel();
        return new Simulation(model.Hull.CreateInitialState(input, headingRad), new Hunter235MassModel(), model, input);
    }
}
