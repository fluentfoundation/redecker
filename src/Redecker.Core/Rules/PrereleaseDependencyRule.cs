using NuGet.Versioning;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports a stable package that depends on a prerelease.
/// </summary>
/// <remarks>
/// <para>
/// Opting into prereleases is a decision a repository makes deliberately — <c>--prerelease</c> on
/// the command line, or a version range that admits them. That decision governs what you reference
/// <em>directly</em>. It does not govern what your dependencies reference, so a stable package with
/// a prerelease dependency quietly puts prerelease code into a graph that opted out.
/// </para>
/// <para>
/// The consequence is not a failed restore. It is that the guarantee a stable version implies —
/// this API is settled, this package will not be pulled — stops applying somewhere inside your
/// graph, at a depth where nobody is looking. <c>Microsoft.Azure.Workflows.WebJobs.Extension</c>
/// ships stable and depends on <c>Microsoft.Azure.WebJobs.Script.Abstractions 1.0.0-preview</c>.
/// </para>
/// <para>
/// <b>Only the lower bound counts.</b> A range whose <em>upper</em> bound is a prerelease, such as
/// <c>[1.0.0, 2.0.0-preview)</c>, still resolves to a stable version and is not reported — the
/// first pass at this used a pattern over the version string and would have flagged it.
/// </para>
/// <para>
/// A prerelease package depending on a prerelease is ordinary and says nothing, so the rule only
/// looks at packages whose own version is stable.
/// </para>
/// </remarks>
public sealed class PrereleaseDependencyRule : IPackageRule
{
    /// <inheritdoc />
    public string Code => "RDK0012";

    /// <inheritdoc />
    public string Name => "stable package depends on a prerelease";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        if (!NuGetVersion.TryParse(package.Version, out var version) || version.IsPrerelease)
        {
            yield break;
        }

        var prereleases = package.Dependencies()
            .Where(d => d.Range.MinVersion is { IsPrerelease: true })
            .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Id: g.Key, Range: g.First().Range))
            .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (prereleases.Count == 0)
        {
            yield break;
        }

        var named = prereleases
            .Take(3)
            .Select(d => $"{d.Id} {d.Range.MinVersion!.ToNormalizedString()}")
            .ToList();

        yield return new Finding(
            Code,
            FindingSeverity.Warning,
            prereleases.Count == 1
                ? $"a stable package depends on the prerelease {named[0]}"
                : $"a stable package depends on {prereleases.Count} prereleases",
            $"{string.Join(", ", named)}" +
            (prereleases.Count > 3 ? $" and {prereleases.Count - 3} more" : "") +
            (prereleases.Count == 1 ? " is a prerelease." : " are prereleases.") +
            " Opting into prereleases governs what a repository references directly, not what its " +
            "dependencies reference, so anyone installing this gets prerelease code without " +
            "asking for it — and the promise a stable version makes stops applying at a depth " +
            "nobody inspects. Either depend on a stable release, or ship this package as a " +
            "prerelease so the choice is visible to whoever takes it.",
            package.Moniker);
    }
}
