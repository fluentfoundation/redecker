using NUnit.Framework;
using Ratchet.Hints;
using Ratchet.Packages;

namespace Ratchet.Tests;

[TestFixture]
public class HintEvaluatorTests
{
    private sealed class FakeStore : IPackageStore
    {
        private readonly Dictionary<string, Func<PackageArchive>> _packages = new(StringComparer.OrdinalIgnoreCase);

        public FakeStore Add(string id, string version, SyntheticPackage package)
        {
            _packages[$"{id}@{version}"] = () => package.Build(id, version);
            return this;
        }

        public Task<PackageArchive?> GetAsync(string id, string version, CancellationToken cancellationToken) =>
            Task.FromResult(_packages.TryGetValue($"{id}@{version}", out var factory) ? factory() : null);

        public Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(
                _packages.Keys
                    .Where(k => k.StartsWith(id + "@", StringComparison.OrdinalIgnoreCase))
                    .Select(k => k[(id.Length + 1)..])
                    .ToList());
    }

    private static Hint Parse(string label)
    {
        Assert.That(HintParser.TryParse(label, out var hint, out var error), Is.True, error);
        return hint!;
    }

    [Test]
    public async Task Reports_a_pin_as_still_required_while_the_upstream_package_is_broken()
    {
        var store = new FakeStore().Add("Broken", "2.0.0", new SyntheticPackage()
            .WithCopyTargets("buildTransitive/net461/Broken.targets", @"..\..\runtimes\win-arm\native\x.dll"));

        var verdict = await new HintEvaluator(store).EvaluateAsync(
            Parse("upstream-bug: #:package Broken@1.0.0; until: package-assets-intact(Broken@2.0.0)"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
            Assert.That(verdict.Explanation, Does.Contain("dangling asset"));
        });
    }

    [Test]
    public async Task Reports_a_pin_as_retirable_once_upstream_ships_the_missing_asset()
    {
        // This is the loop that makes the scheme worth having: the same condition, re-evaluated
        // later against a fixed package, now tells the maintainer to delete the pin.
        var store = new FakeStore().Add("Broken", "2.0.1", new SyntheticPackage()
            .WithCopyTargets("buildTransitive/net461/Broken.targets", @"..\..\runtimes\win-arm\native\x.dll")
            .With("runtimes/win-arm/native/x.dll"));

        var verdict = await new HintEvaluator(store).EvaluateAsync(
            Parse("upstream-bug: #:package Broken@1.0.0; until: package-assets-intact(Broken@2.0.1)"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.Retirable));
            Assert.That(verdict.Explanation, Does.Contain("remove the pin"));
        });
    }

    [Test]
    public async Task Keeps_a_pin_when_the_target_version_was_never_published()
    {
        var verdict = await new HintEvaluator(new FakeStore()).EvaluateAsync(
            Parse("upstream-bug: #:package X@1.0.0; until: package-assets-intact(X@9.9.9)"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
    }

    [Test]
    public async Task Says_so_when_a_hint_records_no_exit_condition()
    {
        var verdict = await new HintEvaluator(new FakeStore()).EvaluateAsync(
            Parse("api-compat: #:package X@1.0.0"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Status, Is.EqualTo(PinStatus.Undetermined));
            Assert.That(verdict.Explanation, Does.Contain("self-retiring"));
        });
    }

    [Test]
    public async Task Treats_a_structural_pin_as_permanent()
    {
        var verdict = await new HintEvaluator(new FakeStore()).EvaluateAsync(
            Parse("framework-band: #:package Microsoft.Extensions.Logging; until: never"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(verdict.Status, Is.EqualTo(PinStatus.StillRequired));
    }
}
