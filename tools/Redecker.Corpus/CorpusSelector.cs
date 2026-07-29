using System.Text.Json;

namespace Redecker.Corpus;

/// <summary>A package version chosen for examination.</summary>
/// <param name="Id">Package identifier.</param>
/// <param name="Version">The version to fetch.</param>
public sealed record Candidate(string Id, string Version);

/// <summary>
/// Chooses which packages a sweep should look at.
/// </summary>
/// <remarks>
/// The search service stops serving results past roughly 3,100 rows per query, so a prefix sweep
/// is "the most-downloaded N matching this prefix" rather than "every package matching it". That
/// ceiling is reported rather than hidden: a sweep that silently truncates reads as coverage it
/// does not have.
/// </remarks>
public sealed class CorpusSelector(HttpClient http)
{
    private const string SearchUrl = "https://azuresearch-usnc.nuget.org/query";

    /// <summary>Most-downloaded packages overall.</summary>
    public async Task<List<Candidate>> TopAsync(int take)
    {
        var results = new List<Candidate>();

        for (var skip = 0; skip < take; skip += 100)
        {
            var page = await PageAsync("", skip, Math.Min(100, take - skip)).ConfigureAwait(false);
            if (page.Count == 0)
            {
                break;
            }

            results.AddRange(page);
        }

        return results;
    }

    /// <summary>
    /// Every reachable package whose id starts with one of <paramref name="prefixes"/>, optionally
    /// filtered to those published within <paramref name="years"/>.
    /// </summary>
    public async Task<List<Candidate>> ByPrefixAsync(
        IReadOnlyList<string> prefixes, int? years, Action<string> log)
    {
        var seen = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var prefix in prefixes)
        {
            var walked = 0;
            var matched = 0;

            for (var skip = 0; ; skip += 100)
            {
                List<Candidate> page;
                try
                {
                    page = await PageAsync(prefix, skip, 100).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    // The service refuses beyond its paging ceiling rather than returning empty.
                    break;
                }

                if (page.Count == 0)
                {
                    break;
                }

                walked += page.Count;

                foreach (var candidate in page)
                {
                    if (candidate.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        matched++;
                        seen[candidate.Id] = candidate;
                    }
                }
            }

            log($"  {prefix,-14} walked {walked,5} rows, {matched,5} ids match the prefix");
        }

        var candidates = seen.Values.OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (years is null)
        {
            return candidates;
        }

        var cutoff = DateTimeOffset.UtcNow.AddYears(-years.Value);
        log($"  filtering to versions published since {cutoff:yyyy-MM-dd}");

        var kept = new List<Candidate>();
        var unknown = 0;

        foreach (var candidate in candidates)
        {
            var published = await PublishedAsync(candidate).ConfigureAwait(false);
            if (published is null)
            {
                // Keep it and say so. Dropping on a failed lookup would silently shrink the
                // corpus in a way that looks like the filter working.
                unknown++;
                kept.Add(candidate);
            }
            else if (published >= cutoff)
            {
                kept.Add(candidate);
            }
        }

        log($"  {kept.Count} of {candidates.Count} kept ({unknown} had no readable publish date and were kept)");
        return kept;
    }

    private async Task<List<Candidate>> PageAsync(string query, int skip, int take)
    {
        var url = $"{SearchUrl}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}" +
                  "&prerelease=false&sortBy=totalDownloads-desc";

        using var document = JsonDocument.Parse(await http.GetStringAsync(url).ConfigureAwait(false));
        var data = document.RootElement.GetProperty("data");

        var results = new List<Candidate>();
        foreach (var entry in data.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString();
            var version = entry.GetProperty("version").GetString();
            if (id is not null && version is not null)
            {
                results.Add(new Candidate(id, version));
            }
        }

        return results;
    }

    /// <summary>
    /// When the chosen version was published. Only the registration carries this; the search
    /// index does not, which is why this costs a request per package.
    /// </summary>
    private async Task<DateTimeOffset?> PublishedAsync(Candidate candidate)
    {
        try
        {
            var url = $"https://api.nuget.org/v3/registration5-semver1/{candidate.Id.ToLowerInvariant()}/index.json";
            using var document = JsonDocument.Parse(await http.GetStringAsync(url).ConfigureAwait(false));

            var pages = document.RootElement.GetProperty("items");
            var last = pages[pages.GetArrayLength() - 1];

            if (!last.TryGetProperty("items", out var leaves))
            {
                using var paged = JsonDocument.Parse(
                    await http.GetStringAsync(last.GetProperty("@id").GetString()!).ConfigureAwait(false));
                leaves = paged.RootElement.GetProperty("items");
            }

            var leaf = leaves[leaves.GetArrayLength() - 1].GetProperty("catalogEntry");
            return leaf.TryGetProperty("published", out var published) &&
                   published.TryGetDateTimeOffset(out var value)
                ? value
                : null;
        }
        catch
        {
            return null;
        }
    }
}
