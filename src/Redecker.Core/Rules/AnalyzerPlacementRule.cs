using System.Text.RegularExpressions;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports analyzer assemblies packed under a target framework folder, which Roslyn will not load.
/// </summary>
/// <remarks>
/// <para>
/// The <c>analyzers/</c> convention has nothing to do with target frameworks. Assemblies live under
/// <c>analyzers/dotnet/&lt;lang&gt;/</c>, or the older <c>analyzers/&lt;lang&gt;/</c>, optionally
/// with a Roslyn version (<c>analyzers/dotnet/roslyn4.8/cs/</c>) and optionally with a locale
/// subfolder for resource assemblies. A path segment like <c>net8.0</c> or <c>net472</c> means
/// somebody applied the <c>lib/</c> layout here by mistake, and the analyzer is never loaded.
/// </para>
/// <para>
/// Nothing reports that. There is no error, no warning, and no output: the diagnostics you wrote
/// simply never appear, which is indistinguishable from code that has no problems.
/// </para>
/// <para>
/// <b>Deliberately narrow.</b> This does not attempt to assert which layouts are valid — real
/// packages use at least six shapes, and <c>analyzers/dotnet/cs/cs</c> is genuinely ambiguous
/// because that trailing <c>cs</c> is Czech rather than C#. Encoding all of that would restate the
/// convention rather than check it. A target framework moniker appears in none of the valid
/// shapes, so it is the one thing that can be called wrong without a lookup table.
/// </para>
/// <para>
/// Most valuable on a package you are about to publish. A widely-used package with this mistake
/// would have been reported years ago; a package being published for the first time would not.
/// </para>
/// </remarks>
public sealed partial class AnalyzerPlacementRule : IPackageRule
{
    // Anchored and specific: "cs", "vb", "dotnet", "roslyn4.8" and locale folders must not match.
    [GeneratedRegex(@"^(net\d|netstandard\d|netcoreapp\d|uap\d|monoandroid\d|xamarin|portable-)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkPattern();

    /// <inheritdoc />
    public string Code => "RDK0008";

    /// <inheritdoc />
    public string Name => "analyzer under a framework folder";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        var offenders = package.Entries
            .Where(e => e.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase) &&
                        e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(e => (Entry: e, Framework: FrameworkSegment(e)))
            .Where(x => x.Framework is not null)
            .GroupBy(x => x.Framework!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in offenders)
        {
            var files = group.Select(x => x.Entry).OrderBy(e => e, StringComparer.Ordinal).ToList();

            yield return new Finding(
                Code,
                FindingSeverity.Warning,
                $"analyzer assemblies are under a framework folder '{group.Key}'",
                $"Roslyn loads analyzers from analyzers/dotnet/<language>/, optionally with a " +
                $"Roslyn version and a locale, and never from a target framework folder. " +
                $"'{group.Key}' looks like the lib/ layout applied here by mistake, which means " +
                $"{string.Join(", ", files.Take(3))}" +
                (files.Count > 3 ? $" and {files.Count - 3} more" : "") +
                " will never be loaded. Nothing reports this: the diagnostics simply never appear.",
                package.Moniker);
        }
    }

    /// <summary>The first path segment under <c>analyzers/</c> that names a target framework.</summary>
    private static string? FrameworkSegment(string entry) =>
        entry.Split('/')
            .Skip(1)                              // past "analyzers"
            .SkipLast(1)                          // the file name itself is not a folder
            .FirstOrDefault(segment => TargetFrameworkPattern().IsMatch(segment));
}
