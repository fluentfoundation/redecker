using System.Collections.Immutable;
using System.Text.Json;
using NuGet.Frameworks;
using Redecker.Packages;

namespace Redecker.Corpus;

/// <summary>
/// Measures how often an assembly under <c>lib/&lt;tfm&gt;/</c> was actually compiled against a
/// different framework than the folder it sits in.
/// </summary>
/// <remarks>
/// <para>
/// This is a survey, not a rule. Issue #4 proposes a rule and then gates it on evidence — reading
/// PE metadata is a real step up in complexity from treating package entries as opaque bytes, and
/// worth taking on only if the failure it catches is common enough to justify it. So: measure
/// first, decide second.
/// </para>
/// <para>
/// It reads the corpus cache rather than the network. Every package version is immutable, so
/// thousands of them are already sitting on disk from earlier sweeps, and a survey that costs
/// nuget.org nothing can afford to be re-run every time a classification turns out to be wrong.
/// </para>
/// </remarks>
public static class TargetFrameworkSurvey
{
    /// <summary>What comparing one assembly against its folder produced.</summary>
    public enum Verdict
    {
        /// <summary>The folder and the assembly agree.</summary>
        Match,

        /// <summary>The assembly carries no <c>TargetFrameworkAttribute</c> to compare against.</summary>
        NoAttribute,

        /// <summary>Not a managed assembly — a native binary shipped under <c>lib/</c>.</summary>
        Unmanaged,

        /// <summary>The folder name is not a framework NuGet recognises.</summary>
        UnreadableFolder,

        /// <summary>
        /// The folder and the assembly differ, but a project targeting the folder can still load
        /// the assembly — a <c>netstandard2.0</c> build dropped into <c>lib/net8.0/</c>, say.
        /// </summary>
        Compatible,

        /// <summary>
        /// A project targeting the folder cannot load the assembly. This is the finding.
        /// </summary>
        Incompatible,
    }

    /// <summary>One assembly, and what the comparison said about it.</summary>
    /// <param name="Package">The package it came from, as <c>id@version</c>.</param>
    /// <param name="Entry">The full entry path inside the package.</param>
    /// <param name="Folder">The <c>lib/</c> folder name.</param>
    /// <param name="Declared">The <c>TargetFrameworkAttribute</c> value, if any.</param>
    /// <param name="Verdict">The classification.</param>
    /// <param name="Primary">
    /// Whether this is the package's own assembly rather than a bundled dependency. Both are worth
    /// reporting — a folder is a promise about everything in it, no matter who compiled it — but
    /// the distinction changes who has to fix it.
    /// </param>
    /// <param name="Living">
    /// Whether both sides are frameworks anyone still ships to. See <see cref="IsLiving"/>.
    /// </param>
    public sealed record Row(
        string Package,
        string Entry,
        string Folder,
        string? Declared,
        string Verdict,
        bool Primary,
        bool Living);

    /// <summary>
    /// Whether a pairing is between frameworks worth reporting on at all: .NET Framework, .NET
    /// Core / .NET 5+, and .NET Standard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Half of every incompatible pairing found sits in a dead platform — PCL profiles, Silverlight,
    /// Windows Phone, Windows Store, MonoAndroid, Xamarin, UAP, Tizen. Those version schemes are
    /// each their own private joke: <c>MonoAndroid403</c> is an OS version, <c>uap10.0</c> maps to
    /// <c>.NETCore,Version=v5.0</c>, and a PCL profile is a set intersection rather than a version
    /// at all.
    /// </para>
    /// <para>
    /// The comparisons there are not obviously wrong, but nobody can act on them — the tooling that
    /// would republish those packages no longer exists. Reporting them would trade a rule people
    /// fix for a rule people suppress.
    /// </para>
    /// </remarks>
    public static bool IsLiving(string folder, string? declared)
    {
        if (declared is null)
        {
            return false;
        }

        var identifier = declared.Split(',')[0];
        if (identifier is not (".NETFramework" or ".NETCoreApp" or ".NETStandard"))
        {
            return false;
        }

        var name = folder.ToLowerInvariant();
        if (!name.StartsWith("net", StringComparison.Ordinal))
        {
            return false;
        }

        // "netcore45"/"netcore451" are Windows Store, not .NET Core; "netcoreapp" is the real thing.
        return !name.StartsWith("netcore", StringComparison.Ordinal) ||
               name.StartsWith("netcoreapp", StringComparison.Ordinal);
    }

    /// <summary>Runs the survey over every package in the corpus cache.</summary>
    /// <param name="cache">The directory of cached <c>.nupkg</c> files.</param>
    /// <param name="results">Where to write the JSON and Markdown reports.</param>
    public static int Run(string cache, string results)
    {
        var packages = Directory.Exists(cache)
            ? Directory.GetFiles(cache, "*.nupkg").OrderBy(f => f, StringComparer.Ordinal).ToList()
            : [];

        if (packages.Count == 0)
        {
            Console.Error.WriteLine(
                $"No cached packages in {cache}. Run a sweep first so there is something to survey.");
            return 1;
        }

        Console.WriteLine($"Surveying {packages.Count} cached packages for framework folder mismatches.");
        Console.WriteLine();

        var rows = new List<Row>();
        var examined = 0;
        var skipped = 0;

        foreach (var file in packages)
        {
            try
            {
                using var package = PackageArchive.OpenFile(file);
                rows.AddRange(Inspect(package));
                examined++;
            }
            catch (Exception ex)
            {
                skipped++;
                Console.Error.WriteLine($"  unreadable {Path.GetFileName(file)}: {ex.GetType().Name}");
            }

            if (examined % 500 == 0 && examined > 0)
            {
                Console.WriteLine($"  {examined}/{packages.Count} examined");
            }
        }

        Report(rows, examined, skipped);
        Write(rows, examined, skipped, results);
        return 0;
    }

    /// <summary>Compares every assembly a package ships under <c>lib/</c> against its folder.</summary>
    public static IEnumerable<Row> Inspect(PackageArchive package)
    {
        foreach (var entry in package.Entries.OrderBy(e => e, StringComparer.Ordinal))
        {
            if (!IsShippedAssembly(entry))
            {
                continue;
            }

            var folder = entry.Split('/')[1];
            var bytes = package.ReadBytes(entry);
            if (bytes is null)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(entry);
            var primary = string.Equals(name, package.Id, StringComparison.OrdinalIgnoreCase);

            var managed = ManagedAssembly.TryReadTargetFramework(bytes, out var declared);
            var verdict = managed ? Classify(folder, declared) : Verdict.Unmanaged;
            yield return new Row(
                package.Moniker, entry, folder, declared, verdict.ToString(),
                primary, IsLiving(folder, declared));
        }
    }

    /// <summary>
    /// Assemblies a consumer would actually compile against: directly in
    /// <c>lib/&lt;framework&gt;/</c>, and not a satellite.
    /// </summary>
    private static bool IsShippedAssembly(string entry) =>
        entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
        entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
        !entry.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) &&
        entry.Count(c => c == '/') == 2;

    /// <summary>Asks whether a project targeting <paramref name="folder"/> could load the assembly.</summary>
    /// <remarks>
    /// <para>
    /// The first attempt at this compared framework identifiers and version numbers by hand, and
    /// spent most of its output on packages that were fine. A <c>netstandard2.0</c> assembly in
    /// <c>lib/net8.0/</c> is not a mistake — it is the standard way to win nearest-framework
    /// matching. A <c>net45</c> assembly in <c>lib/net452/</c> is not a mistake either.
    /// </para>
    /// <para>
    /// The question that actually matters is the one NuGet asks during restore: a consumer who
    /// picked this folder is targeting that framework, the assembly demands the framework in its
    /// attribute, and the pairing is a bug exactly when the first cannot consume the second. NuGet
    /// ships that answer, so ask it rather than re-deriving it — the version arithmetic, the
    /// netstandard fallback chain, PCL profile contribution, and UAP's mapping to
    /// <c>.NETCore,Version=v5.0</c> are all already in there.
    /// </para>
    /// <para>
    /// Only the framework and version are compared, never the platform. A <c>net8.0-windows</c>
    /// folder holds an assembly whose attribute reads plain <c>.NETCoreApp,Version=v8.0</c>,
    /// because the platform lives in a separate attribute.
    /// </para>
    /// </remarks>
    public static Verdict Classify(string folder, string? declared)
    {
        if (declared is null)
        {
            return Verdict.NoAttribute;
        }

        var folderFramework = NuGetFramework.ParseFolder(folder);
        if (folderFramework.IsUnsupported || folderFramework.IsAgnostic)
        {
            return Verdict.UnreadableFolder;
        }

        NuGetFramework declaredFramework;
        try
        {
            declaredFramework = NuGetFramework.Parse(declared);
        }
        catch (ArgumentException)
        {
            return Verdict.UnreadableFolder;
        }

        if (declaredFramework.IsUnsupported)
        {
            return Verdict.UnreadableFolder;
        }

        if (string.Equals(
                folderFramework.Framework, declaredFramework.Framework, StringComparison.OrdinalIgnoreCase) &&
            folderFramework.Version == declaredFramework.Version)
        {
            return Verdict.Match;
        }

        return DefaultCompatibilityProvider.Instance.IsCompatible(folderFramework, declaredFramework)
            ? Verdict.Compatible
            : Verdict.Incompatible;
    }

    private static void Report(List<Row> rows, int examined, int skipped)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"{examined} packages examined ({skipped} skipped), {rows.Count} assemblies read");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        Console.WriteLine($"{"Verdict",-20} {"All",-8} {"Own assembly",-14} {"Bundled",-8}");
        Console.WriteLine(new string('-', 56));

        foreach (var verdict in Enum.GetNames<Verdict>())
        {
            var all = rows.Count(r => r.Verdict == verdict);
            var own = rows.Count(r => r.Verdict == verdict && r.Primary);
            Console.WriteLine($"{verdict,-20} {all,-8} {own,-14} {all - own,-8}");
        }

        Console.WriteLine();

        var incompatible = rows.Where(r => r.Verdict == nameof(Verdict.Incompatible)).ToList();
        var living = incompatible.Where(r => r.Living).ToList();
        var packages = living.Select(r => r.Package).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Console.WriteLine(
            $"Incompatible: {incompatible.Count} assemblies, of which {living.Count} are between " +
            $"living frameworks and {incompatible.Count - living.Count} sit in a dead platform.");
        Console.WriteLine(
            $"A rule scoped to living frameworks would fire on {packages} of {examined} packages " +
            $"({100.0 * packages / examined:F2}%).");
        Console.WriteLine();

        foreach (var primary in new[] { true, false })
        {
            var hits = living.Where(r => r.Primary == primary).ToList();
            Console.WriteLine(
                $"--- {(primary ? "The package's own assembly" : "A bundled dependency")} ({hits.Count})");
            foreach (var row in hits.OrderBy(r => r.Package, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"    {row.Package,-64} {row.Folder,-16} <- {row.Declared}");
            }

            Console.WriteLine();
        }

        // What the rule has to stay quiet about is the more useful half to be able to read.
        var quiet = rows.Where(r => r.Verdict == nameof(Verdict.Compatible) && r.Living).ToList();
        Console.WriteLine($"--- Compatible but not identical, living frameworks ({quiet.Count}) — must stay silent");
        foreach (var (shape, count) in quiet
                     .GroupBy(r => $"{r.Folder} <- {r.Declared}")
                     .Select(g => (g.Key, g.Count()))
                     .OrderByDescending(x => x.Item2)
                     .ThenBy(x => x.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            Console.WriteLine($"    {count,4}  {shape}");
        }

        Console.WriteLine();
    }

    private static void Write(List<Row> rows, int examined, int skipped, string results)
    {
        Directory.CreateDirectory(results);

        var counts = Enum.GetNames<Verdict>().ToImmutableSortedDictionary(
            verdict => verdict,
            verdict => new
            {
                All = rows.Count(r => r.Verdict == verdict),
                Own = rows.Count(r => r.Verdict == verdict && r.Primary),
            });

        // Every incompatible pairing is kept, and so is every merely-compatible one — the latter is
        // what a rule would have to stay quiet about, so it is the more useful half to be able to
        // read. Matches are counted, not listed, or the file would be megabytes of "this was fine".
        var mismatches = rows
            .Where(r => r.Verdict is nameof(Verdict.Incompatible) or nameof(Verdict.Compatible))
            .OrderBy(r => r.Package, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Entry, StringComparer.Ordinal)
            .ToList();

        var json = JsonSerializer.Serialize(
            new { Examined = examined, Skipped = skipped, Assemblies = rows.Count, Counts = counts, Mismatches = mismatches },
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(Path.Combine(results, "survey-target-framework.json"), json);
        File.WriteAllText(Path.Combine(results, "survey-target-framework.md"), Markdown(rows, examined));

        Console.WriteLine($"Wrote {Path.Combine(results, "survey-target-framework.json")}");
        Console.WriteLine($"Wrote {Path.Combine(results, "survey-target-framework.md")}");
    }

    private static string Markdown(List<Row> rows, int examined)
    {
        var incompatible = rows.Where(r => r.Verdict == nameof(Verdict.Incompatible)).ToList();
        var living = incompatible.Where(r => r.Living).ToList();
        var packages = living.Select(r => r.Package).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var text = new System.Text.StringBuilder();
        text.AppendLine("# Survey: does the `lib/<framework>/` folder match the assembly inside it?");
        text.AppendLine();
        text.AppendLine(
            "Evidence for [issue #4](https://github.com/fluentfoundation/redecker/issues/4), which " +
            "proposes a rule and then gates it on whether the failure is common enough to justify " +
            "parsing PE metadata. Run over the corpus cache with `Redecker.Corpus survey-tfm`.");
        text.AppendLine();
        text.AppendLine($"**{examined} packages, {rows.Count} assemblies under `lib/`.**");
        text.AppendLine();
        text.AppendLine("| Verdict | Assemblies | Package's own | Bundled |");
        text.AppendLine("| --- | ---: | ---: | ---: |");

        foreach (var verdict in Enum.GetNames<Verdict>())
        {
            var all = rows.Count(r => r.Verdict == verdict);
            var own = rows.Count(r => r.Verdict == verdict && r.Primary);
            text.AppendLine($"| {verdict} | {all} | {own} | {all - own} |");
        }

        text.AppendLine();
        text.AppendLine(
            $"Of the {incompatible.Count} incompatible pairings, {living.Count} are between frameworks " +
            $"anyone still ships to; the other {incompatible.Count - living.Count} sit in a dead platform " +
            "— PCL profiles, Silverlight, Windows Phone, Windows Store, MonoAndroid, Xamarin, UAP, Tizen. " +
            "Those comparisons are not wrong, but nobody can act on them, so a rule scoped to living " +
            $"frameworks fires on **{packages} of {examined} packages ({100.0 * packages / examined:F2}%)**.");
        text.AppendLine();
        text.AppendLine("## The findings a rule would produce");
        text.AppendLine();
        text.AppendLine("| Package | Folder | Assembly targets | Whose assembly |");
        text.AppendLine("| --- | --- | --- | --- |");

        foreach (var row in living
                     .OrderBy(r => r.Package, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(r => r.Entry, StringComparer.Ordinal))
        {
            text.AppendLine(
                $"| `{row.Package}` | `{row.Folder}` | `{row.Declared}` | " +
                $"{(row.Primary ? "its own" : "bundled")} |");
        }

        text.AppendLine();
        text.AppendLine("## What it has to stay silent about");
        text.AppendLine();
        text.AppendLine(
            "Folder and assembly differing is not the defect — a project targeting the folder being " +
            "unable to load the assembly is. These pairings differ and are fine, and a rule that " +
            "compared version numbers rather than asking NuGet about compatibility would report " +
            "every one of them.");
        text.AppendLine();
        text.AppendLine("| Count | Folder | Assembly targets |");
        text.AppendLine("| ---: | --- | --- |");

        foreach (var group in rows
                     .Where(r => r.Verdict == nameof(Verdict.Compatible) && r.Living)
                     .GroupBy(r => (r.Folder, r.Declared))
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key.Folder, StringComparer.Ordinal))
        {
            text.AppendLine($"| {group.Count()} | `{group.Key.Folder}` | `{group.Key.Declared}` |");
        }

        return text.ToString();
    }
}
