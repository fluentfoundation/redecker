using Redecker.Findings;
using Redecker.Projects;

namespace Redecker.Rules;

/// <summary>
/// Reports declared package versions that no project actually references directly.
/// </summary>
/// <remarks>
/// <para>
/// Transitive dependencies are an implementation detail of the packages you did choose. They
/// normally have no business appearing in <c>Directory.Packages.props</c> at all. When one does,
/// something happened -- almost always a floor being floated above a vulnerable version, or a
/// conflict being settled -- and that reason is worth exactly as much as it is written down.
/// </para>
/// <para>
/// These entries are unusually prone to outliving their cause. The original problem is invisible
/// from the file: the entry looks like an ordinary dependency, so nobody can tell whether
/// deleting it would reintroduce an advisory or simply tidy up. So it stays, and the upgrade it
/// was deferring never happens.
/// </para>
/// <para>
/// A pin carrying a hint is silent here, which is the point: the rule asks for a reason, not for
/// the pin's removal. Both available answers are good ones -- document why it exists, or delete
/// it because it no longer needs to.
/// </para>
/// </remarks>
public sealed class UndocumentedTransitivePinRule
{
    /// <summary>The stable code this rule raises findings under.</summary>
    public string Code => "RDK0004";

    /// <inheritdoc />
    public string Name => "undocumented transitive pin";

    /// <summary>
    /// Checks declared versions against direct references.
    /// </summary>
    /// <param name="pins">Every pin read across the files being checked.</param>
    /// <remarks>
    /// Returns nothing when no <c>PackageReference</c> was seen at all. Checking a lone
    /// <c>Directory.Packages.props</c> would otherwise report every entry it contains, since
    /// nothing in that file references anything.
    /// </remarks>
    public IEnumerable<Finding> Inspect(IReadOnlyCollection<PackagePin> pins)
    {
        var referenced = pins
            .Where(p => p.ItemType.Equals("PackageReference", StringComparison.Ordinal))
            .Select(p => p.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (referenced.Count == 0)
        {
            yield break;
        }

        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in pins.Where(p => p.ItemType.Equals("PackageVersion", StringComparison.Ordinal)))
        {
            if (referenced.Contains(pin.PackageId) || !reported.Add(pin.PackageId))
            {
                continue;
            }

            // A hint is the whole remedy: it says which of the two situations this is, and when
            // the entry stops being needed.
            if (pin.Hint is not null)
            {
                continue;
            }

            yield return new Finding(
                Code,
                FindingSeverity.Warning,
                $"{pin.PackageId} is given a version but no project references it",
                $"{pin.File}{(pin.Line > 0 ? ":" + pin.Line : "")} declares {pin.PackageId} " +
                $"{pin.Version}, and no PackageReference names it. That is either a transitive " +
                "floor someone raised deliberately -- usually to get above a vulnerable version " +
                "-- or an entry that has outlived whatever needed it. Nothing in the file says " +
                "which, so nobody can safely delete it. Add a hint recording why it is here and " +
                "when it can go, or remove it.",
                pin.PackageId);
        }
    }
}
