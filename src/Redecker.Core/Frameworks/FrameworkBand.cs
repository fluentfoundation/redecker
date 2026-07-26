using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Redecker.Frameworks;

/// <summary>
/// Knows which packages ship in lockstep with the runtime, and therefore have to be versioned per
/// target framework rather than simply moved to the newest release.
/// </summary>
/// <remarks>
/// <para>
/// This is the edge case that makes a generic "bump everything to latest" updater wrong for .NET.
/// A project targeting net8.0 wants <c>Microsoft.Extensions.*</c> 8.0.x; the same package at 9.0.x
/// drags in a newer runtime surface, and for a library it silently raises the floor every consumer
/// must meet. The correct unit of update is therefore not the package but the pair
/// <c>(package, target framework band)</c>.
/// </para>
/// <para>
/// Central package management makes expressing that awkward, because a <c>PackageVersion</c> is
/// global. The honest encoding is a set of target-framework-conditioned <c>PackageVersion</c>
/// items -- which is exactly the shape a naive updater flattens back into one.
/// </para>
/// </remarks>
public static partial class FrameworkBand
{
    [GeneratedRegex(@"^net(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModernTfmPattern();

    private static readonly string[] BandedPrefixes =
    [
        "System.",
        "Microsoft.Extensions.",
        "Microsoft.AspNetCore.",
        "Microsoft.NETCore.",
        "Microsoft.Bcl.",
    ];

    /// <summary>
    /// Whether <paramref name="packageId"/> belongs to a family that ships with the runtime.
    /// </summary>
    public static bool IsBanded(string packageId) =>
        BandedPrefixes.Any(p => packageId.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The major version band a target framework expects, e.g. <c>net8.0</c> gives 8.
    /// Returns <see langword="null"/> for frameworks with no in-box band, such as netstandard2.0.
    /// </summary>
    public static int? BandFor(string targetFramework)
    {
        var match = ModernTfmPattern().Match(targetFramework.Trim());
        return match.Success ? int.Parse(match.Groups["major"].Value) : null;
    }

    /// <summary>
    /// Picks the newest version that stays inside the band a target framework expects.
    /// </summary>
    /// <param name="packageId">The package being updated.</param>
    /// <param name="targetFramework">The framework the version has to suit.</param>
    /// <param name="available">Every published version.</param>
    /// <param name="allowPrerelease">Whether prerelease versions may be chosen.</param>
    /// <returns>The chosen version, or null when nothing is suitable.</returns>
    public static NuGetVersion? HighestInBand(
        string packageId,
        string targetFramework,
        IEnumerable<string> available,
        bool allowPrerelease = false)
    {
        var versions = available
            .Select(v => NuGetVersion.TryParse(v, out var parsed) ? parsed : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .Where(v => allowPrerelease || !v.IsPrerelease)
            .ToList();

        if (versions.Count == 0)
        {
            return null;
        }

        var band = BandFor(targetFramework);
        if (band is null || !IsBanded(packageId))
        {
            return versions.Max();
        }

        // Inside the band if one exists; a banded package with no release in the band is a real
        // signal, so fall back to null rather than quietly jumping bands.
        var inBand = versions.Where(v => v.Major == band.Value).ToList();
        return inBand.Count > 0 ? inBand.Max() : null;
    }
}
