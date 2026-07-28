using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports build folders whose MSBuild files can never be imported, because none is named after
/// the package.
/// </summary>
/// <remarks>
/// <para>
/// NuGet imports exactly <c>&lt;PackageId&gt;.props</c> and <c>&lt;PackageId&gt;.targets</c> from
/// the best-matching build folder. Any other file is only reached if one of those imports it.
/// Ship <c>build/Common.targets</c> in a package called <c>Contoso.Widgets</c> and it is simply
/// never read.
/// </para>
/// <para>
/// The failure is total and silent: restore succeeds, the package installs, and the build logic
/// does nothing at all. There is no warning anywhere, because from NuGet's point of view a
/// package is entitled to ship files nobody imports.
/// </para>
/// </remarks>
public sealed class UnimportableBuildFolderRule : IPackageRule
{
    private static readonly string[] BuildRoots = ["build", "buildTransitive", "buildMultiTargeting"];

    /// <inheritdoc />
    public string Code => "RDK0006";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        // Group by the folder an import would be resolved from: build/, or build/<tfm>/.
        var folders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in package.Entries)
        {
            if (!entry.EndsWith(".props", StringComparison.OrdinalIgnoreCase) &&
                !entry.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var root = entry.Split('/')[0];
            if (!BuildRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var slash = entry.LastIndexOf('/');
            var folder = slash < 0 ? string.Empty : entry[..slash];
            if (!folders.TryGetValue(folder, out var files))
            {
                folders[folder] = files = [];
            }

            files.Add(entry[(slash + 1)..]);
        }

        foreach (var (folder, files) in folders.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var expectedProps = package.Id + ".props";
            var expectedTargets = package.Id + ".targets";

            if (files.Any(f => f.Equals(expectedProps, StringComparison.OrdinalIgnoreCase) ||
                               f.Equals(expectedTargets, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return new Finding(
                Code,
                FindingSeverity.Error,
                $"{folder}/ ships MSBuild files that can never be imported",
                $"NuGet imports only {expectedProps} or {expectedTargets} from a build folder. " +
                $"{folder}/ contains {string.Join(", ", files.OrderBy(f => f, StringComparer.Ordinal))}, " +
                "none of which matches the package id, and nothing else imports them. Restore " +
                "succeeds and the build logic silently does nothing.",
                package.Moniker);
        }
    }
}
