using NUnit.Framework;
using Ratchet.Findings;
using Ratchet.Packages;
using Ratchet.Rules;

namespace Ratchet.Tests;

/// <summary>
/// The case this tool was written for, checked against the real packages rather than a synthetic
/// stand-in. These need nuget.org, so they carry a category the CI job can exclude when offline.
/// </summary>
[TestFixture]
[Category("Network")]
public class RealPackageRegressionTests
{
    private FlatContainerPackageStore _store = null!;

    [OneTimeSetUp]
    public void SetUp() =>
        _store = new FlatContainerPackageStore(
            cacheDirectory: Path.Combine(Path.GetTempPath(), "ratchet-test-cache"));

    [OneTimeTearDown]
    public void TearDown() => _store.Dispose();

    [Test]
    public async Task Catches_the_dangling_win_arm_reference_in_SQLitePCLRaw_2_1_12()
    {
        // 2.1.12 stopped shipping runtimes/win-arm/native/e_sqlite3.dll but kept copying it from
        // buildTransitive/net461. Restore succeeds; every net48 build fails with MSB3030.
        using var package = await _store.GetAsync(
            "SQLitePCLRaw.lib.e_sqlite3", "2.1.12", TestContext.CurrentContext.CancellationToken);
        Assert.That(package, Is.Not.Null);

        var findings = new DanglingAssetRule().Inspect(package!).ToList();

        Assert.That(findings, Is.Not.Empty, "the known-bad package should produce a finding");
        Assert.Multiple(() =>
        {
            Assert.That(findings, Has.All.Property(nameof(Finding.Severity)).EqualTo(FindingSeverity.Error));
            Assert.That(
                findings.Any(f => f.Title.Contains("win-arm", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                "expected the win-arm native asset to be named");
        });
    }

    [Test]
    public async Task Clears_the_version_that_is_actually_in_use()
    {
        using var package = await _store.GetAsync(
            "SQLitePCLRaw.lib.e_sqlite3", "2.1.11", TestContext.CurrentContext.CancellationToken);
        Assert.That(package, Is.Not.Null);

        Assert.That(new DanglingAssetRule().Inspect(package!), Is.Empty);
    }

    [Test]
    public async Task Reports_the_native_assets_that_2_1_12_dropped()
    {
        var token = TestContext.CurrentContext.CancellationToken;
        using var before = await _store.GetAsync("SQLitePCLRaw.lib.e_sqlite3", "2.1.11", token);
        using var after = await _store.GetAsync("SQLitePCLRaw.lib.e_sqlite3", "2.1.12", token);

        var findings = new AssetLossRule().Compare(before!, after!).ToList();

        Assert.That(findings, Is.Not.Empty);
        Assert.That(findings[0].Title, Does.Contain("win-arm"));
    }
}
