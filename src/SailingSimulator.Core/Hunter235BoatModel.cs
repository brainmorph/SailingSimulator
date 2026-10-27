namespace SailingSimulator.Core;

/// <summary>Experimental Hunter force composition; not yet a player-ready or validated boat.</summary>
public sealed class Hunter235BoatModel : IRigDynamicsModel
{
    public GravityModel Gravity { get; } = new();
    public Hunter235HullModel Hull { get; } = new();
    public Hunter235Foils Foils { get; } = new();
    public Hunter235RigModel Rig { get; } = new();
    public RigState InitialRig => Rig.InitialRig;

    public ForceMoment Evaluate(in ForceEvaluation evaluation) => EvaluateDynamics(in evaluation).Forces;

    public (ForceMoment Forces, RigDerivative Rig) EvaluateDynamics(in ForceEvaluation evaluation)
    {
        var sail = Rig.EvaluateDynamics(in evaluation);
        return (Gravity.Evaluate(in evaluation) + Hull.Evaluate(in evaluation)
            + Foils.Centerboard.Evaluate(in evaluation) + Foils.Rudder.Evaluate(in evaluation) + sail.Forces, sail.Rig);
    }
}

/// <summary>Creates an experimental Hunter simulation; presentation diagnostics are not yet available.</summary>
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
