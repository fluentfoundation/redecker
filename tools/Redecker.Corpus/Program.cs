using System.Diagnostics;
using System.Text.Json;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Corpus;

/// <summary>
/// Runs every single-package rule across the most-downloaded packages on nuget.org, and records
/// what each one produced.
/// </summary>
/// <remarks>
/// <para>
/// This exists to answer a question ten hand-picked control packages cannot: is a rule safe? A
/// rule firing on 4% of widely-used packages is either finding something important or is wrong,
/// and knowing which before release is worth more than another unit test. On its first run it
/// found three false positives that had already shipped.
/// </para>
/// <para>
/// Results are written to <c>results/</c> and committed, so a later run diffs against them. That
/// is the part that makes this worth keeping rather than a thing someone ran once.
/// </para>
/// <para>
/// Deliberately not part of the shipped tool. <c>redecker inspect</c> should stay something you
/// point at one package.
/// </para>
/// </remarks>
public static class Program
{
    private const string SearchUrl = "https://azuresearch-usnc.nuget.org/query";
    private const string Corpus = "nuget.org, ranked by total downloads";

    public static async Task<int> Main(string[] args)
    {
        var take = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 100;
        var results = args.Length > 1 ? args[1] : "results";

        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redecker-corpus");
        Directory.CreateDirectory(cache);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("redecker-corpus (rule validation sweep)");

        Console.WriteLine($"Sweeping the top {take} packages by download count.");
        Console.WriteLine($"Cache:   {cache}");
        Console.WriteLine($"Results: {Path.GetFullPath(results)}");
        Console.WriteLine();

        var packages = await TopPackagesAsync(http, take).ConfigureAwait(false);
        Console.WriteLine($"Resolved {packages.Count} package versions.\n");

        var rules = AllRules().ToList();
        var recorded = new List<PackageResult>();
        var skipped = 0;
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
                skipped++;
                Console.Error.WriteLine($"  skip {id}@{version}: {ex.GetType().Name}");
                continue;
            }

            using var package = PackageArchive.Open(id, version, new MemoryStream(bytes, writable: false));

            var findings = rules
                .SelectMany(r => r.Inspect(package))
                .Select(f => new RecordedFinding(f.Code, f.Severity.ToString(), f.Title))
                .ToList();

            recorded.Add(new PackageResult(id, version, findings));

            if (recorded.Count % 50 == 0)
            {
                Console.WriteLine($"  {recorded.Count}/{packages.Count} examined ({stopwatch.Elapsed.TotalSeconds:F0}s)");
            }
        }

        var result = new SweepResult(
            Corpus,
            take,
            recorded.Count,
            skipped,
            // Recorded so an empty result is interpretable: "nothing fired" means something only
            // when you know what was looking.
            rules.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal).ToList(),
            recorded.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList());

        Report(result, stopwatch.Elapsed);
        result.Write(results);
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

        // The search service caps a page at 1000; page with skip for larger sweeps.
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

        // Only pause on a real fetch; a cached sweep runs at full speed.
        await Task.Delay(120).ConfigureAwait(false);
        return bytes;
    }

    private static void Report(SweepResult result, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"Examined {result.Examined} packages in {elapsed.TotalSeconds:F0}s ({result.Skipped} skipped)");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        Console.WriteLine($"{"Rule",-12} {"Packages",-10} {"Rate",-8} Reading");
        Console.WriteLine(new string('-', 78));

        foreach (var code in result.Rules)
        {
            var hits = result.Packages.Count(p => p.Findings.Any(f => f.Code == code));
            var rate = result.Examined == 0 ? 0 : 100.0 * hits / result.Examined;
            var reading = SweepResult.Reading(rate).Replace("**", "", StringComparison.Ordinal);
            Console.WriteLine($"{code,-12} {hits,-10} {rate,6:F1}%  {reading}");
        }

        Console.WriteLine();
        foreach (var (code, packages) in result.ByRule())
        {
            Console.WriteLine($"--- {code}");
            foreach (var package in packages.Take(10))
            {
                foreach (var finding in package.Findings.Where(f => f.Code == code))
                {
                    Console.WriteLine($"    {package.Id}@{package.Version}  {finding.Title}");
                }
            }

            Console.WriteLine();
        }
    }
}
