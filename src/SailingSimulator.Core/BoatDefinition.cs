namespace SailingSimulator.Core;

/// <summary>Creates an independent simulation for a particular boat configuration.</summary>
public interface IBoatDefinition
{
    string Id { get; }
    Simulation CreateSimulation(SimulationInput input, double headingRad = 0);
}

/// <summary>The existing provisional ILCA 7 model and mass configuration.</summary>
public sealed class IlcaBoatDefinition : IBoatDefinition
{
    public string Id => "ilca-7";

    public Simulation CreateSimulation(SimulationInput input, double headingRad = 0)
    {
        ArgumentNullException.ThrowIfNull(input);
        var model = new IlcaBoatModel();
        return new Simulation(model.CreateInitialState(input, headingRad), new IlcaMassModel(), model, input);
    }
}
