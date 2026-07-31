using NUnit.Framework;
using Redecker.Hints;
using Redecker.Issues;
using Redecker.Packages;

namespace Redecker.Tests;

[TestFixture]
public class IssueConditionTests
{
    private const string Nuspec =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <package><metadata>
          <id>Upstream.Package</id><version>1.0.0</version>
          <repository type="git" url="https://github.com/acme/upstream" />
        </metadata></package>
        """;

    private sealed class FakeStore : IPackageStore
    {
        public Task<PackageArchive?> GetAsync(string id, string version, CancellationToken ct) =>
            Task.FromResult<PackageArchive?>(
                new SyntheticPackage().With($"{id}.nuspec", Nuspec).Build(id, version));

        // No fake symbol packages: these tests exercise hints, not symbol coverage.
        public Task<PackageArchive?> GetSymbolsAsync(string id, string version, CancellationToken ct) =>
            Task.FromResult<PackageArchive?>(null);

        public Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["1.0.0"]);
    }

    private sealed class FakeTracker : IIssueTracker
    {
        private readonly Dictionary<int, IssueState> _issues = [];
        private readonly Dictionary<string, string> _tags = [];

        public int CompareCalls { get; private set; }

        public FakeTracker Issue(IssueState state)
        {
            _issues[state.Number] = state;
            return this;
        }

        public FakeTracker ReleasedIn(string sha, string tag)
        {
            _tags[sha] = tag;
            return this;
        }

        public Task<IssueState?> GetIssueAsync(GitHubSlug repo, int number, CancellationToken ct) =>
            Task.FromResult(_issues.GetValueOrDefault(number));

        public Task<string?> FirstTagContainingAsync(GitHubSlug repo, string sha, CancellationToken ct)
        {
            CompareCalls++;
            return Task.FromResult(_tags.GetValueOrDefault(sha));
        }
    }

    private static Hint Parse(string label)
    {
        Assert.That(HintParser.TryParse(label, out var hint, out var error), Is.True, error);
        return hint!;
    }

    private static Task<PinVerdict> Evaluate(FakeTracker tracker, string label) =>
        new HintEvaluator(new FakeStore(), tracker)
            .EvaluateAsync(Parse(label), TestContext.CurrentContext.CancellationToken);

    [Test]
    public async Task Holds_the_pin_while_an_issue_is_open()
    {
        var tracker = new FakeTracker().Issue(new IssueState(42, IssueResolution.Open, "Broken on net48"));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(42)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            Assert.That(verdict.Explanation, Does.Contain("#42"));
            Assert.That(verdict.Explanation, Does.Contain("still open"));
        });
    }

    [Test]
    public async Task Retires_the_pin_once_the_issue_is_closed_as_completed()
    {
        var tracker = new FakeTracker().Issue(new IssueState(42, IssueResolution.Completed, "Fixed"));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(42)");

        Assert.That(verdict.Status, Is.EqualTo(PinStatus.Retirable));
    }

    [Test]
    public async Task Keeps_the_pin_when_an_issue_is_closed_as_not_planned()
    {
        // The tracker is tidy, but upstream has declined to fix it. Treating this as resolved
        // would take an upgrade that still carries the defect the pin guards against.
        var tracker = new FakeTracker().Issue(new IssueState(42, IssueResolution.NotPlanned, "Wontfix"));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(42)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            Assert.That(verdict.Explanation, Does.Contain("not planned"));
        });
    }

    [Test]
    public async Task Requires_every_listed_issue_not_merely_one()
    {
        var tracker = new FakeTracker()
            .Issue(new IssueState(1, IssueResolution.Completed))
            .Issue(new IssueState(2, IssueResolution.Open));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(1, 2)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            // The already-resolved one is still reported, so progress is visible.
            Assert.That(verdict.Explanation, Does.Contain("Already resolved"));
        });
    }

    [Test]
    public async Task Closed_is_not_enough_when_the_hint_asks_for_released()
    {
        var tracker = new FakeTracker()
            .Issue(new IssueState(7, IssueResolution.Completed, "Fixed", ClosingCommitSha: "abc12345"));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-released(7)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            Assert.That(verdict.Explanation, Does.Contain("not in any release tag yet"));
        });
    }

    [Test]
    public async Task Retires_once_the_fix_reaches_a_tag()
    {
        var tracker = new FakeTracker()
            .Issue(new IssueState(7, IssueResolution.Completed, "Fixed", "8.0.0", true, "abc12345"))
            .ReleasedIn("abc12345", "v8.0.0");

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-released(7)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.Retirable));
            Assert.That(verdict.Explanation, Does.Contain("v8.0.0"));
            Assert.That(verdict.Explanation, Does.Contain("milestone '8.0.0'"));
        });
    }

    [Test]
    public async Task Does_not_guess_when_a_closed_issue_has_no_closing_commit()
    {
        // Closed by hand. Nothing links it to a release, so claiming it shipped would be a guess.
        var tracker = new FakeTracker().Issue(new IssueState(7, IssueResolution.Completed, "Fixed"));

        var verdict = await Evaluate(tracker,
            "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-released(7)");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            Assert.That(verdict.Explanation, Does.Contain("no closing commit"));
        });
    }

    [Test]
    public async Task Never_asks_about_tags_when_only_closure_was_requested()
    {
        // issues-closed must not pay for release resolution it does not need.
        var tracker = new FakeTracker()
            .Issue(new IssueState(7, IssueResolution.Completed, "Fixed", ClosingCommitSha: "abc12345"));

        await Evaluate(tracker, "upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(7)");

        Assert.That(tracker.CompareCalls, Is.Zero);
    }

    [Test]
    public async Task Says_so_when_no_tracker_is_available()
    {
        var verdict = await new HintEvaluator(new FakeStore()).EvaluateAsync(
            Parse("upstream-bug: #:package Upstream.Package@1.0.0; until: issues-closed(1)"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.Undetermined));
            Assert.That(verdict.Explanation, Does.Contain("--github-token"));
        });
    }

    [TestCase("https://github.com/acme/upstream", "acme", "upstream")]
    [TestCase("https://github.com/acme/upstream.git", "acme", "upstream")]
    [TestCase("git://github.com/acme/upstream.git", "acme", "upstream")]
    [TestCase("git@github.com:acme/upstream.git", "acme", "upstream")]
    [TestCase("https://github.com/acme/upstream/", "acme", "upstream")]
    public void Parses_the_repository_forms_that_appear_in_nuspecs(string url, string owner, string name)
    {
        var slug = GitHubSlug.TryParse(url);

        Assert.That(slug, Is.EqualTo(new GitHubSlug(owner, name)));
    }

    [TestCase("https://gitlab.com/acme/upstream")]
    [TestCase("https://www.newtonsoft.com/json")]
    [TestCase("")]
    [TestCase(null)]
    public void Declines_urls_that_are_not_github_repositories(string? url)
    {
        Assert.That(GitHubSlug.TryParse(url), Is.Null);
    }

    [Test]
    public void Reads_the_repository_url_out_of_a_packages_nuspec()
    {
        using var package = new SyntheticPackage()
            .With("Upstream.Package.nuspec", Nuspec)
            .Build("Upstream.Package", "1.0.0");

        Assert.That(package.RepositoryUrl(), Is.EqualTo("https://github.com/acme/upstream"));
    }

    [Test]
    public void Falls_back_to_projectUrl_only_when_it_names_a_source_host()
    {
        using var github = new SyntheticPackage()
            .With("X.nuspec", "<package><metadata><projectUrl>https://github.com/a/b</projectUrl></metadata></package>")
            .Build();
        using var docs = new SyntheticPackage()
            .With("X.nuspec", "<package><metadata><projectUrl>https://example.com/docs</projectUrl></metadata></package>")
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(github.RepositoryUrl(), Is.EqualTo("https://github.com/a/b"));
            Assert.That(docs.RepositoryUrl(), Is.Null);
        });
    }

    [TestCase("issues-closed(12, 34)", false)]
    [TestCase("issues-released(#12, #34)", true)]
    public void Parses_both_issue_conditions_including_hashed_numbers(string condition, bool released)
    {
        var parsed = HintParser.TryParse(
            $"upstream-bug: #:package X@1.0.0; until: {condition}", out var hint, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True, error);
            Assert.That(hint!.Exit, Is.EqualTo(new ExitCondition.IssuesResolved([12, 34], released)));
        });
    }

    [TestCase("issues-closed(abc)", "not an issue number")]
    [TestCase("issues-closed()", "at least one issue number")]
    public void Rejects_a_malformed_issue_list(string condition, string expected)
    {
        HintParser.TryParse($"upstream-bug: #:package X@1.0.0; until: {condition}", out _, out var error);

        Assert.That(error, Does.Contain(expected));
    }
}
