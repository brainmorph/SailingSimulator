namespace SailingSimulator.Core;

/// <summary>One provisional straight-line sheet, evaluated in hull-datum coordinates.</summary>
public readonly record struct HunterSheetEvaluation(double LengthM, double SlackLengthM,
    Vector3d ClewBodyM, Vector3d LeadBodyM, Vector3d ForceOnClewBodyN, double TorqueAboutForestayNm);

/// <summary>
/// Sheet-geometry experiment; only connected when explicitly selected for Hunter engineering checks.
/// Dimensions and spring stiffness are estimates, not measured rigging.
/// </summary>
public static class Hunter235SheetExperiment
{
    private static readonly Vector3d Tack = new(3.0908, 0, -0.45);
    private static readonly Vector3d Head = new(0.5, 0, -8.2224);
    private static readonly Vector3d Axis = (Head - Tack) / (Head - Tack).Length;
    private const double FootM = 2.1679;
    private const double StiffnessNPerM = 1000;

    /// <param name="port">True for the lead on the port side.</param>
    /// <param name="trim">Normalized sheet tension control, 0 eased to 1 trimmed.</param>
    /// <param name="angleRad">Jib opening, positive toward starboard.</param>
    public static HunterSheetEvaluation Evaluate(bool port, double trim, double angleRad)
    {
        if (!double.IsFinite(trim) || trim < 0 || trim > 1)
            throw new ArgumentOutOfRangeException(nameof(trim));
        if (!double.IsFinite(angleRad)) throw new ArgumentOutOfRangeException(nameof(angleRad));
        var lead = new Vector3d(-1, port ? -0.85 : 0.85, -0.35);
        Vector3d Offset(double angle) => Quaterniond.FromAxisAngle(Axis, angle).Rotate(new(-FootM, 0, 0));
        // The straight sheet is just taut at +1.45 rad when eased and -0.30 rad
        // when fully trimmed (signs reversed for starboard). These are estimates.
        var tautAngle = (port ? 1 : -1) * (1.45 - 1.75 * trim);
        var offset = Offset(angleRad);
        var line = Tack + offset - lead;
        var length = line.Length;
        var slackLength = (Tack + Offset(tautAngle) - lead).Length;
        var extension = Math.Max(0, length - slackLength);
        var force = length > 1e-10 ? line * (-StiffnessNPerM * extension / length) : Vector3d.Zero;
        return new(length, slackLength, Tack + offset, lead, force, Vector3d.Dot(Vector3d.Cross(offset, force), Axis));
    }
}
