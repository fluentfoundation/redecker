using Redecker.Findings;
using Redecker.Frameworks;
using Redecker.Projects;

namespace Redecker.Rules;

/// <summary>
/// Reports families whose packages must carry one version but do not.
/// </summary>
/// <remarks>
/// <para>
/// This is a different constraint from banding, and conflating the two loses it. Banding says a
/// package must match the <em>target framework</em>; lockstep says a set of packages must match
/// <em>each other</em>, whatever version that is. EF Core states it plainly:
/// </para>
/// <para>
/// "Make sure to install the same version of all EF Core packages shipped by Microsoft. For
/// example, if version 5.0.3 of Microsoft.EntityFrameworkCore.SqlServer is installed, then all
/// other Microsoft.EntityFrameworkCore.* packages must also be at 5.0.3."
/// </para>
/// <para>
/// A mismatch here is exactly the kind of thing an automatic updater creates: it bumps whichever
/// family members happen to have newer releases, splits the set, and restore still succeeds.
/// </para>
/// </remarks>
public sealed class LockstepFamilyRule(BandPolicy? policy = null)
{
    private readonly BandPolicy _policy = policy ?? BandPolicy.Default;

    /// <summary>The stable code this rule raises findings under.</summary>
    public string Code => "RDK0003";

    /// <summary>Checks the declared versions of every lockstep family.</summary>
    public IEnumerable<Finding> Inspect(IEnumerable<PackagePin> pins)
    {
        var families = new Dictionary<string, List<PackagePin>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in pins)
        {
            if (pin.Version is null)
            {
                // A PackageReference with no version is governed by central package management;
                // the PackageVersion item carries the constraint and is checked instead.
                continue;
            }

            var family = _policy.LockstepFamily(pin.PackageId);
            if (family is null)
            {
                continue;
            }

            if (!families.TryGetValue(family, out var members))
            {
                families[family] = members = [];
            }

            members.Add(pin);
        }

        foreach (var (family, members) in families.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var versions = members
                .Select(m => m.Version!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();

            if (versions.Count <= 1)
            {
                continue;
            }

            var detail = string.Join(
                ", ",
                members.OrderBy(m => m.PackageId, StringComparer.Ordinal)
                       .Select(m => $"{m.PackageId} {m.Version}"));

            yield return new Finding(
                Code,
                FindingSeverity.Error,
                $"{family}* packages are split across {versions.Count} versions: " +
                string.Join(", ", versions),
                $"Every package in this family must carry the same version. Declared: {detail}. " +
                "Restore will succeed regardless, so this surfaces at run time as a missing " +
                "type or a provider that does not match the core package.",
                family + "*");
        }
    }
}
