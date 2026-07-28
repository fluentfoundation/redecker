using System.Diagnostics;
using System.Text.Json;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Corpus;

/// <summary>
/// Runs every single-package rule across the most-downloaded packages on nuget.org, and reports
/// how often each fires.
/// </summary>
/// <remarks>
/// <para>
/// This exists to answer a question ten hand-picked control packages cannot: is a rule safe? A
/// rule firing on 4% of widely-used packages is either finding something important or is wrong,
/// and knowing which before release is worth more than another unit test.
/// </para>
/// <para>
/// It is deliberately not part of the shipped tool. `redecker inspect` should stay something you
/// point at one package.
/// </para>
/// </remarks>
public static class Program
{
    private const string SearchUrl = "https://azuresearch-usnc.nuget.org/query";

    public static async Task<int> Main(string[] args)
    {
        var take = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 100;
        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redecker-corpus");
        Directory.CreateDirectory(cache);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("redecker-corpus (rule validation sweep)");

        Console.WriteLine($"Sweeping the top {take} packages by download count.");
        Console.WriteLine($"Cache: {cache}");
        Console.WriteLine();

        var packages = await TopPackagesAsync(http, take).ConfigureAwait(false);
        Console.WriteLine($"Resolved {packages.Count} package versions.\n");

        var findingsByRule = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var examined = 0;
        var failed = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var (id, version) in packages)
        {
            byte[] bytes;
            try
            {
                bytes = await DownloadAsync(http, cache, id, version).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"  skip {id}@{version}: {ex.GetType().Name}");
                continue;
            }

            examined++;
            using var package = PackageArchive.Open(id, version, new MemoryStream(bytes, writable: false));

            foreach (var finding in AllRules().SelectMany(r => r.Inspect(package)))
            {
                if (!findingsByRule.TryGetValue(finding.Code, out var hits))
                {
                    findingsByRule[finding.Code] = hits = [];
                }

                hits.Add($"{id}@{version}  {finding.Title}");
            }

            if (examined % 25 == 0)
            {
                Console.WriteLine($"  {examined}/{packages.Count} examined ({stopwatch.Elapsed.TotalSeconds:F0}s)");
            }
        }

        Report(examined, failed, findingsByRule, stopwatch.Elapsed);
        return 0;
    }

    private static IEnumerable<IPackageRule> AllRules() =>
    [
        new DanglingAssetRule(),
        new ToolPackageRule(),
        new UnimportableBuildFolderRule(),
        new UntrackedOutputCopyRule(),
    ];

    /// <summary>Most-downloaded package ids, with their newest stable version.</summary>
    private static async Task<List<(string Id, string Version)>> TopPackagesAsync(HttpClient http, int take)
    {
        var results = new List<(string, string)>();

        // The search service caps a page at 1000, so page with skip for larger sweeps.
        for (var skip = 0; skip < take; skip += 100)
        {
            var page = Math.Min(100, take - skip);
            var url = $"{SearchUrl}?q=&skip={skip}&take={page}&sortBy=totalDownloads-desc&prerelease=false";

            using var document = JsonDocument.Parse(
                await http.GetStringAsync(url).ConfigureAwait(false));

            var data = document.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var entry in data.EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString();
                var version = entry.GetProperty("version").GetString();
                if (id is not null && version is not null)
                {
                    results.Add((id, version));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Fetches a package, caching it on disk. Package versions are immutable, so a hit is always
    /// valid — and repeat sweeps cost nuget.org nothing.
    /// </summary>
    private static async Task<byte[]> DownloadAsync(HttpClient http, string cache, string id, string version)
    {
        var file = Path.Combine(cache, $"{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg");
        if (File.Exists(file))
        {
            return await File.ReadAllBytesAsync(file).ConfigureAwait(false);
        }

        var url = $"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/" +
                  $"{version.ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg";

        var bytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);

        var temp = file + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
        File.Move(temp, file, overwrite: true);

        // Only pause on a real fetch. A cached sweep runs at full speed.
        await Task.Delay(120).ConfigureAwait(false);
        return bytes;
    }

    private static void Report(
        int examined, int failed, Dictionary<string, List<string>> findings, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"Examined {examined} packages in {elapsed.TotalSeconds:F0}s ({failed} skipped)");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();

        if (findings.Count == 0)
        {
            Console.WriteLine("No rule fired on any package.");
            return;
        }

        Console.WriteLine($"{"Rule",-12} {"Packages",-10} {"Rate",-8} Interpretation");
        Console.WriteLine(new string('-', 78));

        foreach (var (code, hits) in findings.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var distinct = hits.Select(h => h.Split("  ")[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var rate = examined == 0 ? 0 : 100.0 * distinct / examined;

            // A high rate on widely-used packages is far likelier to mean the rule is wrong than
            // that the ecosystem is broken. Say so rather than presenting a number.
            var reading = rate switch
            {
                > 20 => "SUSPECT - too common to be a real defect",
                > 5 => "review - unusually common",
                _ => "plausible",
            };

            Console.WriteLine($"{code,-12} {distinct,-10} {rate,6:F1}%  {reading}");
        }

        Console.WriteLine();
        foreach (var (code, hits) in findings.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"--- {code}, first 10 of {hits.Count}");
            foreach (var hit in hits.Take(10))
            {
                Console.WriteLine($"    {hit}");
            }

            Console.WriteLine();
        }
    }
}
