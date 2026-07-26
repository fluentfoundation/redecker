using Ratchet.Findings;
using Ratchet.Packages;

namespace Ratchet.Rules;

/// <summary>A check that can be made against a single package version in isolation.</summary>
public interface IPackageRule
{
    /// <summary>The stable code this rule raises findings under.</summary>
    string Code { get; }

    /// <summary>Runs the check.</summary>
    IEnumerable<Finding> Inspect(PackageArchive package);
}

/// <summary>A check that only means something when comparing two versions of a package.</summary>
public interface IUpgradeRule
{
    /// <summary>The stable code this rule raises findings under.</summary>
    string Code { get; }

    /// <summary>Runs the check.</summary>
    /// <param name="from">The version currently referenced.</param>
    /// <param name="to">The version being considered.</param>
    IEnumerable<Finding> Compare(PackageArchive from, PackageArchive to);
}
