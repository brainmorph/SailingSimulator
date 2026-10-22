namespace SailingSimulator.Core;

/// <summary>Net world-frame force in N and body-frame moment about the centre of mass in N m.</summary>
public readonly record struct ForceMoment(Vector3d ForceWorldN, Vector3d MomentBodyNm)
{
    public bool IsFinite => ForceWorldN.IsFinite && MomentBodyNm.IsFinite;
    public static ForceMoment operator +(ForceMoment a, ForceMoment b) =>
        new(a.ForceWorldN + b.ForceWorldN, a.MomentBodyNm + b.MomentBodyNm);

    /// <summary>Convert a force applied at a body-frame offset from the centre of mass.</summary>
    public static ForceMoment AtBodyPoint(Vector3d forceBodyN, Vector3d offsetFromCenterOfMassBodyM, Quaterniond orientationBodyToWorld) =>
        new(orientationBodyToWorld.Normalized().Rotate(forceBodyN), Vector3d.Cross(offsetFromCenterOfMassBodyM, forceBodyN));
}

public readonly record struct ForceEvaluation(double TimeSeconds, RigidBodyState Boat, MassProperties Mass, SimulationInput Input);

/// <summary>Evaluate all RK4 stages without mutating model state or consuming randomness.</summary>
public interface IForceModel
{
    ForceMoment Evaluate(in ForceEvaluation evaluation);
}

/// <summary>Add independent force sources. No implicit gravity, water, or damping.</summary>
public sealed class CompositeForceModel : IForceModel
{
    private readonly IForceModel[] models;
    public CompositeForceModel(params IForceModel[] models)
    {
        ArgumentNullException.ThrowIfNull(models);
        if (models.Any(model => model is null)) throw new ArgumentException("Force models must be non-null.", nameof(models));
        this.models = (IForceModel[])models.Clone();
    }
    public ForceMoment Evaluate(in ForceEvaluation evaluation)
    {
        var total = default(ForceMoment);
        foreach (var model in models)
        {
            var contribution = model.Evaluate(in evaluation);
            if (!contribution.IsFinite) throw new ArithmeticException($"Non-finite contribution from {model.GetType().Name}.");
            total += contribution;
        }
        return total;
    }
}
