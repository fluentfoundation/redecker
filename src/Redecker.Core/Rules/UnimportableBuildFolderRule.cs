using System.Xml.Linq;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports MSBuild files in a package that nothing inside the package can reach.
/// </summary>
/// <remarks>
/// <para>
/// NuGet imports exactly <c>&lt;PackageId&gt;.props</c> and <c>&lt;PackageId&gt;.targets</c> from
/// the build folder it selects. Everything else is reachable only if one of those imports it,
/// directly or transitively. A file that is neither is never opened through the ordinary package
/// path: restore succeeds, the package installs, and the build logic does nothing, with no
/// diagnostic anywhere.
/// </para>
/// <para>
/// Reachability needs the import graph, not a naming convention. Real packages organise themselves
/// in both directions — Grpc.Tools puts entry points at <c>build/</c> and helpers in
/// <c>build/_grpc/</c>; Win2D puts a shared helper at <c>build/Win2D.common.targets</c> and imports
/// it from <c>build/win10/</c>. Judging either by file name alone accuses both.
/// </para>
/// <para>
/// <b>Known limitation.</b> A file can also be imported from outside the package — by the .NET SDK,
/// by a workload, by another package, or by a consumer writing an explicit <c>Import</c>. None of
/// that is visible here, so SDK-shipped packages such as Microsoft.NET.Sdk.Razor and
/// Microsoft.DotNet.ILCompiler produce findings they do not deserve. There is no reliable marker
/// separating them: ILCompiler carries no <c>packageType</c> and no <c>Sdk/</c> folder, yet its
/// targets are imported by the publish pipeline.
/// </para>
/// <para>
/// That is why this is a warning rather than an error, and why it earns its place on the package
/// you are about to publish rather than on somebody else's. If you wrote the package, you know
/// whether the SDK imports it; the rule cannot.
/// </para>
/// </remarks>
public sealed class UnimportableBuildFolderRule : IPackageRule
{
    private static readonly string[] BuildRoots = ["build", "buildTransitive", "buildMultiTargeting"];

    /// <inheritdoc />
    public string Code => "RDK0006";

    /// <inheritdoc />
    public string Name => "unimportable build file";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        var files = package.Entries
            .Where(e => e.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                        e.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .Where(e => BuildRoots.Contains(e.Split('/')[0], StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (files.Count == 0)
        {
            yield break;
        }

        var entryPoints = files.Where(f => IsEntryPoint(package.Id, f)).ToList();

        if (entryPoints.Count == 0)
        {
            // Nothing can be imported at all. Report per folder rather than per file, since the
            // fix is one correctly named entry point, not renaming everything.
            foreach (var folder in files.Select(Folder).Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Warning,
                    $"{folder}/ ships MSBuild files with no entry point named after the package",
                    $"NuGet imports only {package.Id}.props or {package.Id}.targets from a build " +
                    $"folder, and this package contains neither anywhere. {folder}/ holds " +
                    $"{string.Join(", ", files.Where(f => Folder(f) == folder).Select(Leaf).OrderBy(x => x, StringComparer.Ordinal))}. " +
                    "Unless something outside the package imports these by path — the SDK, a " +
                    "workload, or a consumer writing an explicit Import — they will never be " +
                    "opened, and restore will succeed regardless.",
                    package.Moniker);
            }

            yield break;
        }

        var reachable = new HashSet<string>(entryPoints, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(entryPoints);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var text = package.ReadText(current);
            if (text is null)
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(text);
            }
            catch (System.Xml.XmlException)
            {
                // RDK0001 reports unparseable files; here it only means the graph is unknown.
                yield break;
            }

            foreach (var import in document.Descendants()
                         .Where(e => e.Name.LocalName.Equals("Import", StringComparison.Ordinal))
                         .Select(e => e.Attributes()
                             .FirstOrDefault(a => a.Name.LocalName.Equals("Project", StringComparison.Ordinal))
                             ?.Value)
                         .Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                if (!TryResolveImport(import!, Folder(current), out var target))
                {
                    // Any import this rule cannot evaluate could reach anything in the package,
                    // so nothing can be called unreachable afterwards. This previously bailed
                    // only when the path mentioned MSBuildThisFileDirectory, which missed the
                    // commonest form by far -- a path held entirely in a property, as
                    // CommunityToolkit.Mvvm and Nuke.Common both use.
                    yield break;
                }

                if (files.Contains(target) && reachable.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        // A file can also be handed to MSBuild through an extension-point property rather than an
        // Import — Verify sets its .AfterMicrosoftNetSdk.props that way. Naming the file anywhere
        // in reachable build logic is enough to establish intent, and treating that as reachable
        // costs a true positive far less often than accusing a working package costs credibility.
        var named = reachable
            .Select(package.ReadText)
            .Where(text => text is not null)
            .ToList();

        foreach (var candidate in files.Except(reachable, StringComparer.OrdinalIgnoreCase).ToList())
        {
            var leaf = Leaf(candidate);
            if (named.Any(text => text!.Contains(leaf, StringComparison.OrdinalIgnoreCase)))
            {
                reachable.Add(candidate);
            }
        }

        foreach (var orphan in files.Except(reachable, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            yield return new Finding(
                Code,
                FindingSeverity.Warning,
                $"{orphan} is not imported by anything inside the package",
                $"NuGet imports only {package.Id}.props or {package.Id}.targets from a build " +
                "folder, and nothing that is imported goes on to import this file. If the SDK, a " +
                "workload, or your consumers import it by path then this is expected; otherwise " +
                "it ships and is never opened.",
                package.Moniker);
        }
    }

    private static bool IsEntryPoint(string packageId, string entry)
    {
        var leaf = Leaf(entry);
        return leaf.Equals(packageId + ".props", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals(packageId + ".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static string Folder(string entry)
    {
        var slash = entry.LastIndexOf('/');
        return slash < 0 ? string.Empty : entry[..slash];
    }

    private static string Leaf(string entry) => entry[(entry.LastIndexOf('/') + 1)..];

    /// <summary>Resolves an import to a package entry path, or gives up.</summary>
    internal static bool TryResolveImport(string import, string directory, out string resolved)
    {
        resolved = string.Empty;
        var value = import.Replace('\\', '/').Trim();

        const string thisFileDirectory = "$(MSBuildThisFileDirectory)";
        var index = value.IndexOf(thisFileDirectory, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            value = value[(index + thisFileDirectory.Length)..];
        }
        else if (value.Contains("$(", StringComparison.Ordinal))
        {
            // Anchored outside the package, such as $(MSBuildToolsPath).
            return false;
        }

        if (value.Contains("$(", StringComparison.Ordinal) || value.Contains('*'))
        {
            return false;
        }

        var segments = new List<string>(directory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    break;
                case "..":
                    if (segments.Count == 0)
                    {
                        return false;
                    }

                    segments.RemoveAt(segments.Count - 1);
                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        if (segments.Count == 0)
        {
            return false;
        }

        resolved = string.Join('/', segments);
        return true;
    }
}
