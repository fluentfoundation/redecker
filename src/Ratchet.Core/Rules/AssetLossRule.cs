using Ratchet.Findings;
using Ratchet.Packages;

namespace Ratchet.Rules;

/// <summary>
/// Reports target frameworks and runtime identifiers that a package used to ship and no longer
/// does.
/// </summary>
/// <remarks>
/// Losing an asset is not automatically wrong -- packages legitimately retire dead platforms --
/// but it silently changes what a consumer gets. A project multi-targeting net48 discovers that
/// the new version dropped its <c>lib/net461</c> asset only when compilation picks a different
/// one, and a project that ships a win-arm build discovers a dropped RID only at run time on that
/// device. Both are worth a human decision rather than an automatic merge, which is why these are
/// warnings and the caller decides whether the lost asset is one it actually targets.
/// </remarks>
public sealed class AssetLossRule : IUpgradeRule
{
    /// <inheritdoc />
    public string Code => "RATCHET0002";

    /// <inheritdoc />
    public IEnumerable<Finding> Compare(PackageArchive from, PackageArchive to)
    {
        foreach (var finding in Compare(
            from.LibFrameworks(), to.LibFrameworks(), "target framework", "lib", from, to))
        {
            yield return finding;
        }

        foreach (var finding in Compare(
            from.RuntimeIdentifiers(), to.RuntimeIdentifiers(), "runtime identifier", "runtimes", from, to))
        {
            yield return finding;
        }
    }

    private IEnumerable<Finding> Compare(
        IReadOnlySet<string> before,
        IReadOnlySet<string> after,
        string noun,
        string folder,
        PackageArchive from,
        PackageArchive to)
    {
        var lost = before.Except(after, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (lost.Count == 0)
        {
            yield break;
        }

        yield return new Finding(
            Code,
            FindingSeverity.Warning,
            $"{from.Version} to {to.Version} drops {lost.Count} {noun}{(lost.Count == 1 ? "" : "s")}: " +
            string.Join(", ", lost),
            $"{from.Moniker} ships {folder}/ entries for {string.Join(", ", lost)}; {to.Moniker} does not. " +
            $"Consumers selecting {(lost.Count == 1 ? "that " + noun : "those " + noun + "s")} will " +
            "resolve a different asset, or none at all.",
            to.Moniker);
    }
}
