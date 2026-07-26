using NUnit.Framework;
using Ratchet.Findings;
using Ratchet.Rules;

namespace Ratchet.Tests;

[TestFixture]
public class AssetLossRuleTests
{
    [Test]
    public void Reports_a_dropped_runtime_identifier()
    {
        using var before = new SyntheticPackage()
            .With("runtimes/win-x64/native/x.dll")
            .With("runtimes/win-arm/native/x.dll")
            .Build(version: "2.1.11");
        using var after = new SyntheticPackage()
            .With("runtimes/win-x64/native/x.dll")
            .Build(version: "2.1.12");

        var findings = new AssetLossRule().Compare(before, after).ToList();

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Title, Does.Contain("win-arm"));
            Assert.That(findings[0].Title, Does.Contain("runtime identifier"));
        });
    }

    [Test]
    public void Reports_a_dropped_target_framework()
    {
        using var before = new SyntheticPackage()
            .With("lib/net461/x.dll")
            .With("lib/netstandard2.0/x.dll")
            .Build(version: "1.0.0");
        using var after = new SyntheticPackage()
            .With("lib/netstandard2.0/x.dll")
            .Build(version: "2.0.0");

        var findings = new AssetLossRule().Compare(before, after).ToList();

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("net461"));
    }

    [Test]
    public void Is_silent_when_assets_are_added_but_none_removed()
    {
        using var before = new SyntheticPackage().With("lib/net8.0/x.dll").Build();
        using var after = new SyntheticPackage()
            .With("lib/net8.0/x.dll")
            .With("lib/net10.0/x.dll")
            .Build();

        Assert.That(new AssetLossRule().Compare(before, after), Is.Empty);
    }

    [Test]
    public void Groups_all_losses_of_one_kind_into_a_single_finding()
    {
        using var before = new SyntheticPackage()
            .With("runtimes/win-arm/native/x.dll")
            .With("runtimes/win10-x64/native/x.dll")
            .With("runtimes/win-x64/native/x.dll")
            .Build();
        using var after = new SyntheticPackage().With("runtimes/win-x64/native/x.dll").Build();

        var findings = new AssetLossRule().Compare(before, after).ToList();

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("drops 2 runtime identifiers"));
    }
}
