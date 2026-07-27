using System.Text.RegularExpressions;

namespace Redecker.Issues;

/// <summary>An <c>owner/name</c> pair identifying a GitHub repository.</summary>
/// <param name="Owner">The account or organisation.</param>
/// <param name="Name">The repository name.</param>
public sealed partial record GitHubSlug(string Owner, string Name)
{
    [GeneratedRegex(@"github\.com[/:](?<owner>[^/]+)/(?<name>[^/#?]+?)(?:\.git)?/?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    /// <summary>
    /// Extracts a slug from a repository URL. Handles the https, git and scp-style forms that
    /// turn up in nuspec metadata, and the trailing <c>.git</c> that some publishers include.
    /// </summary>
    public static GitHubSlug? TryParse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = UrlPattern().Match(url.Trim());
        return match.Success
            ? new GitHubSlug(match.Groups["owner"].Value, match.Groups["name"].Value)
            : null;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Owner}/{Name}";
}

/// <summary>Why an issue was closed. GitHub distinguishes these, and so must we.</summary>
public enum IssueResolution
{
    /// <summary>Still open.</summary>
    Open,

    /// <summary>
    /// Closed as completed: someone did the work. Only this discharges a pin.
    /// </summary>
    Completed,

    /// <summary>
    /// Closed as not planned. The issue is gone from the tracker but the underlying problem is
    /// not fixed, so a pin waiting on it is still doing its job. Treating this as "resolved"
    /// would silently take an upgrade that upstream has explicitly declined to fix.
    /// </summary>
    NotPlanned,
}

/// <summary>The state of one upstream issue.</summary>
/// <param name="Number">The issue number.</param>
/// <param name="Resolution">Open, completed, or declined.</param>
/// <param name="Title">The issue title, for reporting.</param>
/// <param name="Milestone">The milestone title, when one is assigned.</param>
/// <param name="MilestoneClosed">Whether that milestone has been closed.</param>
/// <param name="ClosingCommitSha">The commit that closed it, when GitHub recorded one.</param>
public sealed record IssueState(
    int Number,
    IssueResolution Resolution,
    string? Title = null,
    string? Milestone = null,
    bool MilestoneClosed = false,
    string? ClosingCommitSha = null);

/// <summary>Somewhere upstream issue state can be read from.</summary>
public interface IIssueTracker
{
    /// <summary>Reads one issue, or returns null when it cannot be found.</summary>
    Task<IssueState?> GetIssueAsync(GitHubSlug repository, int number, CancellationToken cancellationToken);

    /// <summary>
    /// The earliest release tag that contains <paramref name="commitSha"/>, or null when no tag
    /// does yet.
    /// </summary>
    Task<string?> FirstTagContainingAsync(
        GitHubSlug repository, string commitSha, CancellationToken cancellationToken);
}
