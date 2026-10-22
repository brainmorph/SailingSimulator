namespace SailingSimulator.Core;

/// <summary>Double-precision vector. Frame and units belong to the containing member.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static Vector3d Zero => default;
    public static Vector3d UnitX => new(1, 0, 0);
    public static Vector3d UnitY => new(0, 1, 0);
    public static Vector3d UnitZ => new(0, 0, 1);
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
    public double LengthSquared => Dot(this, this);
    public double Length => Math.Sqrt(LengthSquared);
    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3d Cross(Vector3d a, Vector3d b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(double s, Vector3d a) => a * s;
    public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);
}
