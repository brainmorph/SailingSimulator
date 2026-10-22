namespace SailingSimulator.Core;

/// <summary>Symmetric inertia matrix (off-diagonal entries include their physical sign).</summary>
public readonly record struct InertiaTensor(double XX, double YY, double ZZ, double XY, double XZ, double YZ)
{
    public Vector3d Multiply(Vector3d v) => new(XX * v.X + XY * v.Y + XZ * v.Z,
        XY * v.X + YY * v.Y + YZ * v.Z, XZ * v.X + YZ * v.Y + ZZ * v.Z);

    // Cholesky factorization also rejects indefinite tensors without hiding bad mass data.
    public void Validate() => Factor();
    private (double A, double B, double C, double D, double E, double F) Factor()
    {
        var a = Math.Sqrt(XX);
        var b = XY / a;
        var c = XZ / a;
        var d = Math.Sqrt(YY - b * b);
        var e = (YZ - c * b) / d;
        var f = Math.Sqrt(ZZ - c * c - e * e);
        if (!double.IsFinite(a + b + c + d + e + f) || a <= 0 || d <= 0 || f <= 0)
            throw new ArgumentException("Inertia must be finite and positive definite.");
        return (a, b, c, d, e, f);
    }

    public Vector3d Solve(Vector3d moment)
    {
        var (a, b, c, d, e, f) = Factor();
        var u = moment.X / a;
        var v = (moment.Y - b * u) / d;
        var w = (moment.Z - c * u - e * v) / f;
        var z = w / f;
        var y = (v - e * z) / d;
        return new((u - b * y - c * z) / a, y, z);
    }

    public static InertiaTensor operator +(InertiaTensor a, InertiaTensor b) =>
        new(a.XX + b.XX, a.YY + b.YY, a.ZZ + b.ZZ, a.XY + b.XY, a.XZ + b.XZ, a.YZ + b.YZ);

    public static InertiaTensor PointMass(double mass, Vector3d r) => new(
        mass * (r.Y * r.Y + r.Z * r.Z), mass * (r.X * r.X + r.Z * r.Z),
        mass * (r.X * r.X + r.Y * r.Y), -mass * r.X * r.Y, -mass * r.X * r.Z, -mass * r.Y * r.Z);
}

public sealed record MassContributor(string Name, double MassKg, Vector3d CenterBodyM, InertiaTensor IntrinsicInertiaKgM2);

/// <summary>Pure mass geometry sampled once per step. Total mass must remain constant.</summary>
public interface IMassModel
{
    MassProperties Evaluate(SailingControls controls);
}

/// <summary>Provisional 58 kg equipped budget plus separate 80 kg sailor. See docs/boat-physics.md.</summary>
public sealed class IlcaMassModel : IMassModel
{
    public IReadOnlyList<MassContributor> Contributors(SailingControls controls) => Array.AsReadOnly(new[]
    {
        new MassContributor("Hull", 42, new(0, 0, 0.03), new(7, 48, 53, 0, 0, 0)),
        new MassContributor("Rig", 9, new(0.35, 0, -2.1), new(24, 27, 4, 0, 0, 0)),
        new MassContributor("Centerboard", 2.5, new(0.05, 0, 0.35 - 0.65 * (1 - controls.Centerboard)), new(0.18, 0.2, 0.025, 0, 0, 0)),
        new MassContributor("Rudder", 1.5, new(-2, 0, 0.28), new(0.04, 0.05, 0.02, 0, 0, 0)),
        new MassContributor("Fittings", 3, new(-0.2, 0, -0.05), new(0.1, 1, 1, 0, 0, 0)),
        // Sailor is one point mass. Positive hiking places it to starboard.
        new MassContributor("Sailor", 80, new(0, 0.85 * controls.Hiking, -0.32 - 0.12 * Math.Abs(controls.Hiking)), default)
    });

    public MassProperties Evaluate(SailingControls controls)
    {
        var contributors = Contributors(controls);
        var mass = contributors.Sum(c => c.MassKg);
        var center = Vector3d.Zero;
        foreach (var c in contributors) center += c.MassKg * c.CenterBodyM;
        center /= mass;
        var inertia = default(InertiaTensor);
        foreach (var c in contributors)
            inertia += c.IntrinsicInertiaKgM2 + InertiaTensor.PointMass(c.MassKg, c.CenterBodyM - center);
        return MassProperties.FromTensor(mass, center, inertia);
    }
}
