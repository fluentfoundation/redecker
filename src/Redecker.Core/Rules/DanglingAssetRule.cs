using System.Xml.Linq;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Finds files that a package's own MSBuild logic points at but that the package does not ship.
/// </summary>
/// <remarks>
/// <para>
/// This is the check that motivated the tool. SQLitePCLRaw.lib.e_sqlite3 2.1.12 stopped shipping
/// <c>runtimes/win-arm/native/e_sqlite3.dll</c>, but its
/// <c>buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets</c> still lists that file for
/// copying. The upgrade restores cleanly, resolves cleanly, and builds cleanly on every target
/// except net48, where it fails with MSB3030. No amount of version-graph reasoning finds that,
/// because nothing about it is expressed in the dependency graph -- but reading the package does.
/// </para>
/// <para>
/// The resolver deliberately only reports paths it is certain about. A reference is skipped when
/// it still contains an unexpanded MSBuild property or item metadata, or a wildcard, because the
/// value then depends on evaluation context this rule does not have. Reporting only what can be
/// resolved keeps the rule usable as a gate: a finding here means a file really is missing.
/// </para>
/// </remarks>
public sealed class DanglingAssetRule : IPackageRule
{
    /// <inheritdoc />
    public string Code => "RDK0001";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        foreach (var file in package.MsBuildFiles())
        {
            var text = package.ReadText(file);
            if (text is null)
            {
                continue;
            }

            var document = TryParse(text, out var parseError);
            if (document is null)
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Info,
                    $"Could not parse {file}",
                    $"The package ships {file}, but it is not well-formed XML ({parseError}). " +
                    "Its references could not be checked.",
                    package.Moniker);
                continue;
            }

            var directory = DirectoryOf(file);

            foreach (var reference in ReferencedPaths(document))
            {
                if (!TryResolve(reference, directory, out var resolved))
                {
                    continue;
                }

                if (package.Contains(resolved))
                {
                    continue;
                }

                yield return new Finding(
                    Code,
                    FindingSeverity.Error,
                    $"{file} references {resolved}, which the package does not contain",
                    $"MSBuild imports {file} into every consuming project that matches its folder, " +
                    $"and that file points at '{reference}'. The package ships no such entry, so a " +
                    "consumer whose target framework selects this file fails at build time even " +
                    "though restore succeeds.",
                    package.Moniker);
            }
        }
    }

    /// <summary>
    /// Parses an MSBuild file, reporting failure through <paramref name="error"/> rather than
    /// throwing, so that one malformed file does not abandon the rest of the package.
    /// </summary>
    private static XDocument? TryParse(string text, out string? error)
    {
        try
        {
            error = null;
            return XDocument.Parse(text);
        }
        catch (System.Xml.XmlException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Every value in the document that could name a file relative to the MSBuild file itself.
    /// </summary>
    private static IEnumerable<string> ReferencedPaths(XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                // Conditions contain comparisons rather than paths; treating them as paths
                // produces noise like "the package does not contain 'build/net461/'".
                if (attribute.Name.LocalName.Equals("Condition", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var value in Split(attribute.Value))
                {
                    if (!IsGuardedByExists(element, value))
                    {
                        yield return value;
                    }
                }
            }

            // Item metadata written as a child element, e.g. <Link>runtimes/...</Link>.
            if (!element.HasElements)
            {
                foreach (var value in Split(element.Value))
                {
                    if (!IsGuardedByExists(element, value))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether a reference is deliberately optional, because the author guarded it with
    /// <c>Exists(...)</c>.
    /// </summary>
    /// <remarks>
    /// This is how a package offers an extension point. Microsoft.Data.SqlClient.SNI imports a
    /// <c>.targets.user</c> file if the consumer has written one, guarded by
    /// <c>Exists(...)</c> — the file is *meant* to be absent, and reporting it as a dangling
    /// reference turns a deliberate hook into a false accusation.
    /// </remarks>
    private static bool IsGuardedByExists(XElement element, string reference)
    {
        var trimmed = reference.Trim();

        foreach (var scope in element.AncestorsAndSelf())
        {
            var condition = scope.Attributes()
                .FirstOrDefault(a => a.Name.LocalName.Equals("Condition", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            if (condition is null ||
                !condition.Contains("Exists(", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only treat it as guarded when the condition mentions this same path, so an
            // unrelated Exists() elsewhere in the condition does not excuse everything.
            if (condition.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>MSBuild treats semicolons as list separators almost everywhere.</summary>
    private static IEnumerable<string> Split(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string DirectoryOf(string entry)
    {
        var slash = entry.LastIndexOf('/');
        return slash < 0 ? string.Empty : entry[..(slash + 1)];
    }

    /// <summary>
    /// Turns a reference into a package-relative entry path, or gives up.
    /// </summary>
    /// <returns><see langword="true"/> only when the result is certain.</returns>
    internal static bool TryResolve(string reference, string msBuildFileDirectory, out string resolved)
    {
        resolved = string.Empty;

        const string thisFileDirectory = "$(MSBuildThisFileDirectory)";
        var index = reference.IndexOf(thisFileDirectory, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            // Without this anchor the reference is relative to the consuming project, not to the
            // package, so the package cannot be expected to contain it.
            return false;
        }

        var candidate = reference[(index + thisFileDirectory.Length)..];
        candidate = candidate.Replace('\\', '/').Trim();

        // Anything still holding a property or item metadata depends on evaluation context.
        if (candidate.Contains("$(", StringComparison.Ordinal) ||
            candidate.Contains("%(", StringComparison.Ordinal) ||
            candidate.Contains('*') || candidate.Contains('?'))
        {
            return false;
        }

        if (candidate.Length == 0 || candidate.EndsWith('/'))
        {
            return false;
        }

        var combined = Collapse(msBuildFileDirectory + candidate);
        if (combined is null)
        {
            return false;
        }

        // Require a file extension. Directory references are legitimate but are not what breaks a
        // build, and demanding an extension keeps the rule free of false positives.
        var lastSegment = combined[(combined.LastIndexOf('/') + 1)..];
        if (!lastSegment.Contains('.'))
        {
            return false;
        }

        resolved = combined;
        return true;
    }

    /// <summary>
    /// Resolves <c>.</c> and <c>..</c> segments. Returns null if the path escapes the package root,
    /// which means it is not a package asset at all.
    /// </summary>
    private static string? Collapse(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    break;
                case "..":
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.RemoveAt(segments.Count - 1);
                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }
}
