namespace SailingSimulator.Core;

/// <summary>Hamilton quaternion (X,Y,Z,W), active rotation from body to world.</summary>
public readonly record struct Quaterniond(double X, double Y, double Z, double W)
{
    public static Quaterniond Identity => new(0, 0, 0, 1);
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z) && double.IsFinite(W);
    public double LengthSquared => X * X + Y * Y + Z * Z + W * W;

    public Quaterniond Normalized()
    {
        var length = Math.Sqrt(LengthSquared);
        if (!IsFinite || !double.IsFinite(length) || length < 1e-12)
            throw new ArgumentException("Orientation must be finite and have nonzero norm.");
        return this * (1 / length);
    }

    public Quaterniond Conjugate() => new(-X, -Y, -Z, W);

    /// <summary>Rotate with a unit quaternion. The integrator normalizes before frame conversion.</summary>
    public Vector3d Rotate(Vector3d value)
    {
        var q = new Vector3d(X, Y, Z);
        var twiceCross = 2 * Vector3d.Cross(q, value);
        return value + W * twiceCross + Vector3d.Cross(q, twiceCross);
    }

    public static Quaterniond FromAxisAngle(Vector3d axis, double radians)
    {
        var length = axis.Length;
        if (!axis.IsFinite || !double.IsFinite(length) || length < 1e-12 || !double.IsFinite(radians))
            throw new ArgumentException("Axis must be finite and nonzero; angle must be finite.");
        var v = axis * (Math.Sin(radians / 2) / length);
        return new(v.X, v.Y, v.Z, Math.Cos(radians / 2));
    }

    public static Quaterniond operator +(Quaterniond a, Quaterniond b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Quaterniond operator *(Quaterniond a, double s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);
    public static Quaterniond operator *(Quaterniond a, Quaterniond b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
}
