using System.Text;
using Redecker.Issues;
using Redecker.Packages;

namespace Redecker.Hints;

/// <summary>
/// Evaluates <see cref="ExitCondition.IssuesResolved"/> by asking the upstream tracker whether
/// the issues a pin waits on have actually been fixed.
/// </summary>
/// <remarks>
/// The repository is discovered rather than configured: the pinned package's nuspec already
/// declares where its source lives, so a hint states only which issues it waits on. That keeps
/// the hint short, and keeps it correct when a project moves -- the URL comes from whichever
/// package version is pinned.
/// </remarks>
public sealed class IssueConditionEvaluator(IPackageStore packages, IIssueTracker issues)
{
    /// <summary>Evaluates the condition for a pin on <paramref name="hint"/>'s package.</summary>
    public async Task<PinVerdict> EvaluateAsync(
        Hint hint,
        ExitCondition.IssuesResolved condition,
        CancellationToken cancellationToken)
    {
        var slug = await ResolveRepositoryAsync(hint, cancellationToken).ConfigureAwait(false);
        if (slug is null)
        {
            return new PinVerdict(
                PinStatus.Undetermined,
                $"{hint.PackageId} does not declare a GitHub repository in its nuspec, so the " +
                "issues cannot be located. Name the repository in the hint, or check a package " +
                "version that carries <repository url=\"...\"> metadata.");
        }

        var outstanding = new List<string>();
        var satisfied = new List<string>();

        foreach (var number in condition.Issues)
        {
            var issue = await issues.GetIssueAsync(slug, number, cancellationToken).ConfigureAwait(false);
            if (issue is null)
            {
                outstanding.Add($"#{number} could not be read from {slug}");
                continue;
            }

            var label = issue.Title is null ? $"#{number}" : $"#{number} ({Trim(issue.Title)})";

            switch (issue.Resolution)
            {
                case IssueResolution.Open:
                    outstanding.Add($"{label} is still open");
                    continue;

                case IssueResolution.NotPlanned:
                    // Deliberately not a pass. The tracker is tidy but the defect remains, so a
                    // pin guarding against it is still earning its place.
                    outstanding.Add(
                        $"{label} was closed as not planned, so the underlying problem stands");
                    continue;
            }

            if (!condition.RequireReleased)
            {
                satisfied.Add($"{label} closed as completed{Milestone(issue)}");
                continue;
            }

            if (issue.ClosingCommitSha is null)
            {
                // Closed by hand rather than by a commit. Nothing links it to a release, so the
                // honest answer is that we cannot tell.
                outstanding.Add(
                    $"{label} is closed, but GitHub records no closing commit, so whether the " +
                    "fix shipped cannot be determined automatically");
                continue;
            }

            var tag = await issues
                .FirstTagContainingAsync(slug, issue.ClosingCommitSha, cancellationToken)
                .ConfigureAwait(false);

            if (tag is null)
            {
                outstanding.Add(
                    $"{label} is fixed in {issue.ClosingCommitSha[..Math.Min(8, issue.ClosingCommitSha.Length)]} " +
                    "but that commit is not in any release tag yet");
                continue;
            }

            satisfied.Add($"{label} released in {tag}{Milestone(issue)}");
        }

        return Summarise(condition, outstanding, satisfied);
    }

    private static PinVerdict Summarise(
        ExitCondition.IssuesResolved condition,
        IReadOnlyList<string> outstanding,
        IReadOnlyList<string> satisfied)
    {
        if (outstanding.Count > 0)
        {
            var text = new StringBuilder("still blocked: ").AppendJoin("; ", outstanding);
            if (satisfied.Count > 0)
            {
                text.Append(". Already resolved: ").AppendJoin("; ", satisfied);
            }

            return new PinVerdict(PinStatus.StillRequired, text.ToString());
        }

        var verb = condition.RequireReleased ? "released" : "closed as completed";
        return new PinVerdict(
            PinStatus.Retirable,
            $"every issue this pin waits on is {verb} ({string.Join("; ", satisfied)}). " +
            "Remove the pin and take the upgrade.");
    }

    /// <summary>
    /// Finds the package's repository, preferring the exact pinned version so the answer
    /// reflects what is actually referenced.
    /// </summary>
    private async Task<GitHubSlug?> ResolveRepositoryAsync(Hint hint, CancellationToken cancellationToken)
    {
        if (hint.Version is not null)
        {
            using var pinned = await packages
                .GetAsync(hint.PackageId, hint.Version, cancellationToken).ConfigureAwait(false);
            var fromPinned = GitHubSlug.TryParse(pinned?.RepositoryUrl());
            if (fromPinned is not null)
            {
                return fromPinned;
            }
        }

        // Older versions sometimes predate repository metadata; the newest release is the best
        // remaining source for where the project lives now.
        var versions = await packages.GetVersionsAsync(hint.PackageId, cancellationToken)
            .ConfigureAwait(false);
        if (versions.Count == 0)
        {
            return null;
        }

        using var latest = await packages
            .GetAsync(hint.PackageId, versions[^1], cancellationToken).ConfigureAwait(false);
        return GitHubSlug.TryParse(latest?.RepositoryUrl());
    }

    private static string Milestone(IssueState issue) =>
        issue.Milestone is null
            ? string.Empty
            : $", milestone '{issue.Milestone}'{(issue.MilestoneClosed ? " (closed)" : " (still open)")}";

    private static string Trim(string title) =>
        title.Length <= 60 ? title : title[..57] + "...";
}
