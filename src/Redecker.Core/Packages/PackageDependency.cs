using System.Xml.Linq;
using NuGet.Versioning;

namespace Redecker.Packages;

/// <summary>A version range one package declares on another.</summary>
/// <param name="Id">The package depended on.</param>
/// <param name="Range">The range of versions this package accepts.</param>
/// <param name="TargetFramework">
/// The framework group the declaration sits in, or <see langword="null"/> for a flat declaration
/// outside any group.
/// </param>
public sealed record PackageDependency(string Id, VersionRange Range, string? TargetFramework);

/// <summary>Reads the dependencies a nuspec declares.</summary>
/// <remarks>
/// <para>
/// Parsed as XML rather than matched with a regular expression. Nuspecs come in several schema
/// versions with different namespaces, dependencies appear both inside <c>&lt;group&gt;</c>
/// elements and flat beside them, and attribute order is not guaranteed. A regex that got any of
/// that wrong would be Redecker's bug rather than NuGet's, and it would be wrong silently.
/// </para>
/// <para>
/// Elements are matched on local name, ignoring namespace, because the namespace is exactly the
/// part that varies between schema versions and carries no information worth checking.
/// </para>
/// </remarks>
public static class NuspecDependencies
{
    /// <summary>Every dependency declared by a nuspec, in document order.</summary>
    /// <param name="nuspec">The nuspec text, or <see langword="null"/>.</param>
    public static IReadOnlyList<PackageDependency> Read(string? nuspec)
    {
        if (string.IsNullOrWhiteSpace(nuspec))
        {
            return [];
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(nuspec);
        }
        catch (System.Xml.XmlException)
        {
            // A nuspec that will not parse is nuget.org's problem, not something to guess about.
            return [];
        }

        var results = new List<PackageDependency>();

        foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "dependency"))
        {
            var id = (string?)Attribute(element, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            // An omitted version means "any version", which constrains nothing.
            var version = (string?)Attribute(element, "version");
            if (string.IsNullOrWhiteSpace(version) ||
                !VersionRange.TryParse(version, out var range))
            {
                continue;
            }

            var group = element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "group");
            var framework = group is null ? null : (string?)Attribute(group, "targetFramework");

            results.Add(new PackageDependency(id!, range, framework));
        }

        return results;
    }

    /// <summary>An attribute by local name, ignoring namespace and case.</summary>
    private static XAttribute? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(
            a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
}
