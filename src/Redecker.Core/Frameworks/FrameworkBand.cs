using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Redecker.Frameworks;

/// <summary>
/// Version selection for packages that are tied to a target framework's generation.
/// </summary>
/// <remarks>
/// <para>
/// This is the edge case that makes a generic "bump everything to latest" updater wrong for
/// .NET. For a banded package the correct unit of update is not the package but the pair
/// <c>(package, target framework band)</c>: a net8.0 project wants the 8.x line even when 9.x
/// exists, because 9.x is written against a runtime it is not running on.
/// </para>
/// <para>
/// Which packages those are is policy rather than a prefix -- see <see cref="BandPolicy"/>.
/// Central package management then makes expressing the result awkward, because a
/// <c>PackageVersion</c> is global; the honest encoding is a set of
/// target-framework-conditioned <c>PackageVersion</c> items, which is exactly the shape a naive
/// updater flattens back into one.
/// </para>
/// </remarks>
public static partial class FrameworkBand
{
    [GeneratedRegex(@"^net(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModernTfmPattern();

    /// <summary>
    /// Whether <paramref name="packageId"/> is tied to a runtime generation under
    /// <paramref name="policy"/>, defaulting to <see cref="BandPolicy.Default"/>.
    /// </summary>
    public static bool IsBanded(string packageId, BandPolicy? policy = null) =>
        (policy ?? BandPolicy.Default).IsBanded(packageId);

    /// <summary>
    /// The major version band a target framework expects, e.g. <c>net8.0</c> gives 8.
    /// Returns <see langword="null"/> for frameworks with no in-box band, such as
    /// netstandard2.0.
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
    /// <param name="policy">Which packages are banded; defaults to <see cref="BandPolicy.Default"/>.</param>
    /// <returns>The chosen version, or null when nothing is suitable.</returns>
    public static NuGetVersion? HighestInBand(
        string packageId,
        string targetFramework,
        IEnumerable<string> available,
        bool allowPrerelease = false,
        BandPolicy? policy = null)
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
        if (band is null || !IsBanded(packageId, policy))
        {
            // Compile-at-head: most of Microsoft.Extensions.* belongs here, and taking the
            // newest stable release is simply correct.
            return versions.Max();
        }

        // A banded package with no release in the band is a real signal -- a project on a
        // framework the family has not shipped for -- so report nothing rather than quietly
        // jumping generations.
        var inBand = versions.Where(v => v.Major == band.Value).ToList();
        return inBand.Count > 0 ? inBand.Max() : null;
    }
}
