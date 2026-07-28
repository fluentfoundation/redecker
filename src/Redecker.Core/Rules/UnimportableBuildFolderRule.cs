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
        var expectedProps = package.Id + ".props";
        var expectedTargets = package.Id + ".targets";

        bool IsEntryPoint(string fileName) =>
            fileName.Equals(expectedProps, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(expectedTargets, StringComparison.OrdinalIgnoreCase);

        var msbuildFiles = package.Entries
            .Where(e => e.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                        e.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .Where(e => BuildRoots.Contains(e.Split('/')[0], StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var root in BuildRoots)
        {
            var inRoot = msbuildFiles
                .Where(e => e.Split('/')[0].Equals(root, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (inRoot.Count == 0)
            {
                continue;
            }

            // NuGet imports from the build root itself, or from one framework folder beneath it.
            // Nothing deeper is ever an import root, so a package with an entry point at the root
            // may organise the rest however it likes -- Grpc.Tools imports build/_grpc/ and
            // build/_protobuf/ from build/Grpc.Tools.props, which is entirely correct.
            if (inRoot.Any(e => e.Count(c => c == '/') == 1 && IsEntryPoint(e.Split('/')[^1])))
            {
                continue;
            }

            var candidates = inRoot
                .Where(e => e.Count(c => c == '/') == 1)
                .Select(_ => root)
                .Concat(inRoot.Where(e => e.Count(c => c == '/') >= 2)
                              .Select(e => string.Join('/', e.Split('/').Take(2))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e, StringComparer.Ordinal);

            foreach (var folder in candidates)
            {
                var files = inRoot
                    .Where(e => e.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) &&
                                e[(folder.Length + 1)..].Count(c => c == '/') == 0)
                    .Select(e => e.Split('/')[^1])
                    .ToList();

                if (files.Count == 0 || files.Any(IsEntryPoint))
                {
                    continue;
                }

                yield return new Finding(
                    Code,
                    FindingSeverity.Error,
                    $"{folder}/ ships MSBuild files that can never be imported",
                    $"NuGet imports only {expectedProps} or {expectedTargets} from a build folder. " +
                    $"{folder}/ contains {string.Join(", ", files.OrderBy(f => f, StringComparer.Ordinal))}, " +
                    "none of which matches the package id, and no entry point at the root of " +
                    $"{root}/ could import them. Restore succeeds and the build logic silently " +
                    "does nothing.",
                    package.Moniker);
            }
        }
    }
}
