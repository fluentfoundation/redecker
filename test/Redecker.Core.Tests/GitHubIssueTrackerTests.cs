using NUnit.Framework;
using Redecker.Issues;

namespace Redecker.Tests;

/// <summary>
/// Checks release containment against the live GitHub API. The whole claim of
/// <see cref="GitHubIssueTracker.FirstTagContainingAsync"/> is that it answers a question that
/// looks like it needs the commit graph without cloning anything, and only the real API can
/// demonstrate that.
/// </summary>
[TestFixture]
[Category("Network")]
public class GitHubIssueTrackerTests
{
    private static readonly GitHubSlug Repo = new("fluentmigrator", "fluentmigrator");

    // The commits v8.0.0 and v7.2.0 point at. Every tag from that release onwards contains the
    // commit; every earlier tag does not. Tagged commits are immutable, so these are stable.
    private const string V800Commit = "d73e81e49849cf013e1963e1a9be0204f6fb2c6c";
    private const string V720Commit = "ec46621640de573d86b7cdd9e62b50b512b6d8b8";

    private GitHubIssueTracker _tracker = null!;

    [OneTimeSetUp]
    public void SetUp() =>
        _tracker = new GitHubIssueTracker(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));

    [OneTimeTearDown]
    public void TearDown() => _tracker.Dispose();

    [TestCase(V800Commit, "v8.0.0")]
    [TestCase(V720Commit, "v7.2.0")]
    public async Task Finds_the_earliest_release_tag_containing_a_commit(string sha, string expected)
    {
        var tag = await _tracker.FirstTagContainingAsync(
            Repo, sha, TestContext.CurrentContext.CancellationToken);

        // Earliest, not merely any: a later tag also contains the commit, so returning v8.0.1
        // for the v8.0.0 commit would mean the binary search settled on the wrong boundary.
        Assert.That(tag, Is.EqualTo(expected));
    }

    [Test]
    public async Task Reports_a_commit_no_tag_contains_as_unreleased()
    {
        // An unknown SHA cannot be compared against any tag, and the tracker must answer "no
        // release" rather than throwing: an unreleased fix is an ordinary, expected state.
        var tag = await _tracker.FirstTagContainingAsync(
            Repo, new string('0', 40), TestContext.CurrentContext.CancellationToken);

        Assert.That(tag, Is.Null);
    }

    [Test]
    public async Task Reads_issue_state_and_how_it_was_closed()
    {
        var issue = await _tracker.GetIssueAsync(Repo, 1, TestContext.CurrentContext.CancellationToken);

        Assert.That(issue, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(issue!.Number, Is.EqualTo(1));
            Assert.That(issue.Title, Is.Not.Null.And.Not.Empty);
            // Issue #1 of a long-lived project is closed; which way is not this test's business,
            // but "open" would mean the state mapping is broken.
            Assert.That(issue.Resolution, Is.Not.EqualTo(IssueResolution.Open));
        });
    }

    [Test]
    public async Task Reports_an_issue_that_does_not_exist_as_null()
    {
        var issue = await _tracker.GetIssueAsync(
            Repo, 999_999_999, TestContext.CurrentContext.CancellationToken);

        Assert.That(issue, Is.Null);
    }
}
