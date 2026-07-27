using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NuGet.Versioning;

namespace Redecker.Issues;

/// <summary>
/// Reads issue and release state from the GitHub REST API.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here clones anything. The question "has the fix shipped in a release?" looks like it
/// needs the commit graph, and <c>git tag --contains</c> genuinely does, but the REST compare
/// endpoint answers it directly: comparing a tag against a commit reports <c>identical</c> or
/// <c>behind</c> when the tag contains it, and <c>ahead</c> when it does not.
/// </para>
/// <para>
/// That matters for load. A shallow clone of a large repository is tens of megabytes; these are
/// a few kilobytes each. And because containment is monotonic over an ordered release history,
/// the earliest containing tag can be binary searched rather than scanned: on a repository with
/// 72 tags that is 8 requests instead of 73.
/// </para>
/// <para>
/// Budget for context: an authenticated user has 5,000 REST requests an hour, while the
/// <c>GITHUB_TOKEN</c> inside a workflow gets 1,000 per hour per repository. A pin waiting on
/// three issues costs roughly a dozen requests, so a repository would need hundreds of hinted
/// pins before this is worth worrying about.
/// </para>
/// </remarks>
public sealed class GitHubIssueTracker : IIssueTracker, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;

    /// <param name="token">A GitHub token. Without one the limit is 60 requests an hour.</param>
    /// <param name="baseUrl">API base, for GitHub Enterprise.</param>
    /// <param name="httpClient">An HTTP client to borrow.</param>
    public GitHubIssueTracker(
        string? token = null,
        string baseUrl = "https://api.github.com",
        HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("redecker");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <inheritdoc />
    public async Task<IssueState?> GetIssueAsync(
        GitHubSlug repository, int number, CancellationToken cancellationToken)
    {
        var issue = await GetJsonAsync($"/repos/{repository}/issues/{number}", cancellationToken)
            .ConfigureAwait(false);
        if (issue is null)
        {
            return null;
        }

        var root = issue.RootElement;
        var open = root.GetProperty("state").GetString() == "open";

        // state_reason distinguishes "someone fixed it" from "we are not going to". Absent on
        // older closed issues, where completed is the only sensible reading.
        var reason = root.TryGetProperty("state_reason", out var r) ? r.GetString() : null;
        var resolution = open
            ? IssueResolution.Open
            : reason == "not_planned"
                ? IssueResolution.NotPlanned
                : IssueResolution.Completed;

        string? milestone = null;
        var milestoneClosed = false;
        if (root.TryGetProperty("milestone", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            milestone = m.GetProperty("title").GetString();
            milestoneClosed = m.TryGetProperty("state", out var ms) && ms.GetString() == "closed";
        }

        var sha = open
            ? null
            : await ClosingCommitAsync(repository, number, cancellationToken).ConfigureAwait(false);

        return new IssueState(
            number,
            resolution,
            root.TryGetProperty("title", out var t) ? t.GetString() : null,
            milestone,
            milestoneClosed,
            sha);
    }

    /// <summary>
    /// The commit GitHub recorded as closing the issue. Only present when it was closed by a
    /// commit or merged pull request rather than by hand, so a null here is normal.
    /// </summary>
    private async Task<string?> ClosingCommitAsync(
        GitHubSlug repository, int number, CancellationToken cancellationToken)
    {
        var timeline = await GetJsonAsync(
            $"/repos/{repository}/issues/{number}/timeline?per_page=100", cancellationToken)
            .ConfigureAwait(false);
        if (timeline is null || timeline.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? sha = null;
        foreach (var e in timeline.RootElement.EnumerateArray())
        {
            if (e.TryGetProperty("event", out var ev) && ev.GetString() == "closed" &&
                e.TryGetProperty("commit_id", out var c) && c.ValueKind == JsonValueKind.String)
            {
                // Take the last close: an issue reopened and closed again should resolve to the
                // commit that closed it most recently.
                sha = c.GetString();
            }
        }

        return sha;
    }

    /// <inheritdoc />
    public async Task<string?> FirstTagContainingAsync(
        GitHubSlug repository, string commitSha, CancellationToken cancellationToken)
    {
        var tags = await OrderedTagsAsync(repository, cancellationToken).ConfigureAwait(false);
        if (tags.Count == 0)
        {
            return null;
        }

        // Containment is monotonic along an ordered release history: if a tag contains the
        // commit, every later tag does too. That is what licenses the binary search. It can be
        // violated by a fix landing on a maintenance branch before a newer line picks it up, so
        // the boundary is confirmed below rather than assumed.
        var low = 0;
        var high = tags.Count - 1;
        string? found = null;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (await TagContainsAsync(repository, tags[mid], commitSha, cancellationToken)
                .ConfigureAwait(false))
            {
                found = tags[mid];
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return found;
    }

    private async Task<bool> TagContainsAsync(
        GitHubSlug repository, string tag, string commitSha, CancellationToken cancellationToken)
    {
        var comparison = await GetJsonAsync(
            $"/repos/{repository}/compare/{Uri.EscapeDataString(tag)}...{commitSha}", cancellationToken)
            .ConfigureAwait(false);
        if (comparison is null)
        {
            return false;
        }

        // base=tag, head=commit. "ahead" means the commit is ahead of the tag, so the tag was cut
        // before it; "behind" and "identical" both mean the tag already includes it.
        var status = comparison.RootElement.GetProperty("status").GetString();
        return status is "behind" or "identical";
    }

    /// <summary>Tags that parse as versions, oldest first.</summary>
    private async Task<IReadOnlyList<string>> OrderedTagsAsync(
        GitHubSlug repository, CancellationToken cancellationToken)
    {
        var tags = new List<(NuGetVersion Version, string Name)>();

        for (var page = 1; page <= 10; page++)
        {
            var body = await GetJsonAsync(
                $"/repos/{repository}/tags?per_page=100&page={page}", cancellationToken)
                .ConfigureAwait(false);
            if (body is null || body.RootElement.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var count = 0;
            foreach (var t in body.RootElement.EnumerateArray())
            {
                count++;
                var name = t.GetProperty("name").GetString();
                if (name is null)
                {
                    continue;
                }

                // Tolerate the usual v-prefix without demanding a particular convention.
                if (NuGetVersion.TryParse(name.TrimStart('v', 'V'), out var version))
                {
                    tags.Add((version, name));
                }
            }

            if (count < 100)
            {
                break;
            }
        }

        return tags.OrderBy(t => t.Version).Select(t => t.Name).ToList();
    }

    private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(_baseUrl + path, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) &&
            remaining.FirstOrDefault() == "0")
        {
            throw new InvalidOperationException(
                "GitHub API rate limit exhausted. Supply a token with --github-token, or set " +
                "GITHUB_TOKEN, to raise the limit from 60 requests an hour to 5,000.");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
