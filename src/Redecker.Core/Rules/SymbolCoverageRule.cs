using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports assemblies a package ships that its symbol package does not cover.
/// </summary>
/// <remarks>
/// <para>
/// Shipping no symbol package at all is a choice, and this rule says nothing about it. Shipping one
/// that covers some assemblies and not others is almost always an accident: of 58 sampled packages
/// that publish symbols, 57 cover every assembly they ship. Complete coverage is the convention, so
/// a gap is a real signal rather than a matter of taste.
/// </para>
/// <para>
/// Satellite assemblies are excluded. The one package in that sample with a genuine gap was
/// `Microsoft.VisualStudio.Validation`, whose <c>lib/net8.0/de/…resources.dll</c> and its siblings
/// have no PDBs — which is correct, because a resource assembly contains no code to step through.
/// Only assemblies sitting directly in <c>lib/&lt;framework&gt;/</c> are considered.
/// </para>
/// <para>
/// A warning, not an error. Missing symbols degrade debugging; they do not break a build, fail an
/// install, or silently disable anything.
/// </para>
/// </remarks>
public sealed class SymbolCoverageRule
{
    /// <summary>The stable code this rule raises findings under.</summary>
    public string Code => "RDK0009";

    /// <summary>A short human name.</summary>
    public string Name => "incomplete symbol package";

    /// <summary>
    /// Compares a package against its symbol package.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <param name="symbols">
    /// Its <c>.snupkg</c>, or <see langword="null"/> when none was published — in which case there
    /// is nothing to report.
    /// </param>
    public IEnumerable<Finding> Inspect(PackageArchive package, PackageArchive? symbols)
    {
        if (symbols is null)
        {
            yield break;
        }

        var covered = symbols.Entries
            .Where(e => e.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .Select(e => e[..^4])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = package.Entries
            .Where(IsShippedAssembly)
            .Where(e => !covered.Contains(e[..^4]))
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();

        if (uncovered.Count == 0)
        {
            yield break;
        }

        var total = package.Entries.Count(IsShippedAssembly);

        yield return new Finding(
            Code,
            FindingSeverity.Warning,
            $"the symbol package covers {total - uncovered.Count} of {total} shipped assemblies",
            $"{string.Join(", ", uncovered.Take(4))}" +
            (uncovered.Count > 4 ? $" and {uncovered.Count - 4} more" : "") +
            (uncovered.Count == 1 ? " has" : " have") +
            " no matching .pdb in the symbol package. Consumers stepping into " +
            (uncovered.Count == 1 ? "it get" : "those assemblies get") + " no source. " +
            "Publishing no symbols at all is a choice; publishing some is usually an oversight — " +
            "of 58 sampled packages that publish symbols, 57 cover everything they ship.",
            package.Moniker);
    }

    /// <summary>
    /// Assemblies a consumer would step into: directly in <c>lib/&lt;framework&gt;/</c>, and not a
    /// satellite.
    /// </summary>
    /// <remarks>
    /// The depth check excludes locale folders such as <c>lib/net8.0/de/</c> on its own; the name
    /// check is belt and braces for packages that flatten them.
    /// </remarks>
    private static bool IsShippedAssembly(string entry) =>
        entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
        entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
        !entry.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) &&
        entry.Count(c => c == '/') == 2;
}
