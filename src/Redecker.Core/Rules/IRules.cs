using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>A rule that can be identified without a lookup table.</summary>
/// <remarks>
/// A code on its own is not reviewable. Nobody remembers what RDK0006 is, so every place a rule is
/// named — console output, results files, documentation tables — carries the name beside the code.
/// </remarks>
public interface INamedRule
{
    /// <summary>The stable code this rule raises findings under.</summary>
    string Code { get; }

    /// <summary>A short human name, such as "dangling asset reference".</summary>
    string Name { get; }
}

/// <summary>A check that can be made against a single package version in isolation.</summary>
public interface IPackageRule : INamedRule
{
    /// <summary>Runs the check.</summary>
    IEnumerable<Finding> Inspect(PackageArchive package);
}

/// <summary>A check that only means something when comparing two versions of a package.</summary>
public interface IUpgradeRule : INamedRule
{
    /// <summary>Runs the check.</summary>
    /// <param name="from">The version currently referenced.</param>
    /// <param name="to">The version being considered.</param>
    IEnumerable<Finding> Compare(PackageArchive from, PackageArchive to);
}
