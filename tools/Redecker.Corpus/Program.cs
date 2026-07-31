using System.Diagnostics;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Corpus;

/// <summary>
/// Runs every single-package rule across a chosen slice of nuget.org, and records what each one
/// produced.
/// </summary>
/// <remarks>
/// <para>
/// This exists to answer a question ten hand-picked control packages cannot: is a rule safe? A
/// rule firing on 4% of widely-used packages is either finding something important or is wrong,
/// and knowing which before release is worth more than another unit test. It has so far found
/// three false positives that had already shipped, and killed a fourth rule before it was written.
/// </para>
/// <para>
/// Results are written to <c>results/</c> and committed, so a later run diffs against them.
/// </para>
/// </remarks>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("redecker-corpus (rule validation sweep)");

        var selector = new CorpusSelector(http);

        string corpus;
        string name;
        List<Candidate> candidates;
        var results = "results";

        if (args.Length > 0 && args[0].Equals("prefix", StringComparison.OrdinalIgnoreCase))
        {
            var prefixes = (args.Length > 1 ? args[1] : "Microsoft.,System.")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var years = args.Length > 2 && int.TryParse(args[2], out var y) ? y : (int?)null;
            results = args.Length > 3 ? args[3] : results;

            name = "prefix-" + string.Join("-", prefixes.Select(p => p.TrimEnd('.').ToLowerInvariant()));
            corpus = $"nuget.org, ids starting with {string.Join(" or ", prefixes)}" +
                     (years is null ? "" : $", published within {years} years");

            Console.WriteLine($"Selecting packages: {corpus}");
            candidates = await selector.ByPrefixAsync(prefixes, years, Console.WriteLine)
                .ConfigureAwait(false);
        }
        else
        {
            var take = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 100;
            results = args.Length > 1 ? args[1] : results;

            name = $"top-{take}";
            corpus = "nuget.org, ranked by total downloads";
            Console.WriteLine($"Selecting the top {take} packages by download count.");
            candidates = await selector.TopAsync(take).ConfigureAwait(false);
        }

        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redecker-corpus");
        Directory.CreateDirectory(cache);

        Console.WriteLine();
        Console.WriteLine($"{candidates.Count} package versions selected.");
        Console.WriteLine($"Cache:   {cache}");
        Console.WriteLine($"Results: {Path.GetFullPath(results)}");
        Console.WriteLine();

        var rules = AllRules().ToList();
        var recorded = new List<PackageResult>();
        var skipped = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var candidate in candidates)
        {
            byte[] bytes;
            try
            {
                bytes = await DownloadAsync(http, cache, candidate).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                skipped++;
                Console.Error.WriteLine($"  skip {candidate.Id}@{candidate.Version}: {ex.GetType().Name}");
                continue;
            }

            try
            {
                using var package = PackageArchive.Open(
                    candidate.Id, candidate.Version, new MemoryStream(bytes, writable: false));

                var findings = rules
                    .SelectMany(r => r.Inspect(package))
                    .Select(f => new RecordedFinding(f.Code, f.Severity.ToString(), f.Title))
                    .ToList();

                recorded.Add(new PackageResult(candidate.Id, candidate.Version, findings));
            }
            catch (Exception ex)
            {
                skipped++;
                Console.Error.WriteLine($"  unreadable {candidate.Id}@{candidate.Version}: {ex.GetType().Name}");
            }

            if (recorded.Count % 250 == 0)
            {
                Console.WriteLine($"  {recorded.Count}/{candidates.Count} examined ({stopwatch.Elapsed.TotalSeconds:F0}s)");
            }
        }

        var result = new SweepResult(
            corpus,
            candidates.Count,
            recorded.Count,
            skipped,
            rules.OrderBy(r => r.Code, StringComparer.Ordinal).Select(r => r.Code).ToList(),
            rules.OrderBy(r => r.Code, StringComparer.Ordinal).Select(r => r.Name).ToList(),
            recorded.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList());

        Report(result, stopwatch.Elapsed);
        result.Write(results, name);
        return 0;
    }

    private static IEnumerable<IPackageRule> AllRules() =>
    [
        new DanglingAssetRule(),
        new ToolPackageRule(),
        new UnimportableBuildFolderRule(),
        new UntrackedOutputCopyRule(),
        new AnalyzerPlacementRule(),
    ];

    /// <summary>
    /// Fetches a package, caching it on disk. Package versions are immutable, so a hit is always
    /// valid — and repeat sweeps cost nuget.org nothing.
    /// </summary>
    private static async Task<byte[]> DownloadAsync(HttpClient http, string cache, Candidate candidate)
    {
        var id = candidate.Id.ToLowerInvariant();
        var version = candidate.Version.ToLowerInvariant();
        var file = Path.Combine(cache, $"{id}.{version}.nupkg");

        if (File.Exists(file))
        {
            return await File.ReadAllBytesAsync(file).ConfigureAwait(false);
        }

        var url = $"https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg";
        var bytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);

        var temp = file + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
        File.Move(temp, file, overwrite: true);

        // Only pause on a real fetch; a cached sweep runs at full speed.
        await Task.Delay(80).ConfigureAwait(false);
        return bytes;
    }

    private static void Report(SweepResult result, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"Examined {result.Examined} packages in {elapsed.TotalSeconds:F0}s ({result.Skipped} skipped)");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        Console.WriteLine($"{"Rule",-9} {"Name",-32} {"Pkgs",-6} {"Rate",-8} Reading");
        Console.WriteLine(new string('-', 92));

        foreach (var (code, name) in result.Rules.Zip(result.RuleNames))
        {
            var hits = result.Packages.Count(p => p.Findings.Any(f => f.Code == code));
            var rate = result.Examined == 0 ? 0 : 100.0 * hits / result.Examined;
            var reading = SweepResult.Reading(rate).Replace("**", "", StringComparison.Ordinal);
            Console.WriteLine($"{code,-9} {name,-32} {hits,-6} {rate,6:F1}%  {reading}");
        }

        Console.WriteLine();
        foreach (var (code, packages) in result.ByRule())
        {
            Console.WriteLine($"--- {code}  ({packages.Count} packages)");
            foreach (var package in packages.Take(25))
            {
                foreach (var finding in package.Findings.Where(f => f.Code == code))
                {
                    Console.WriteLine($"    {package.Id}@{package.Version}  {finding.Title}");
                }
            }

            if (packages.Count > 25)
            {
                Console.WriteLine($"    ... and {packages.Count - 25} more, see the results file");
            }

            Console.WriteLine();
        }
    }
}
