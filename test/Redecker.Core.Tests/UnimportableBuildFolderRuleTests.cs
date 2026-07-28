using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class UnimportableBuildFolderRuleTests
{
    private static List<Finding> Inspect(SyntheticPackage p, string id = "Contoso.Widgets")
    {
        using var archive = p.Build(id, "1.0.0");
        return new UnimportableBuildFolderRule().Inspect(archive).ToList();
    }

    [Test]
    public void Accepts_files_named_after_the_package()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.props", "<Project />")
            .With("build/Contoso.Widgets.targets", "<Project />"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_a_build_folder_nothing_can_import()
    {
        // Ships, restores, installs, and does absolutely nothing.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Common.targets", "<Project />"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Error));
            Assert.That(findings[0].Detail, Does.Contain("Contoso.Widgets.props"));
            Assert.That(findings[0].Detail, Does.Contain("Common.targets"));
        });
    }

    [Test]
    public void Accepts_a_helper_file_next_to_a_correctly_named_one()
    {
        // The named file is the entry point; anything it imports is legitimate.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets", "<Project />")
            .With("build/Common.targets", "<Project />"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Checks_each_framework_folder_separately()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("buildTransitive/net8.0/Contoso.Widgets.targets", "<Project />")
            .With("buildTransitive/net461/Helper.targets", "<Project />"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("net461"));
    }

    [TestCase("build")]
    [TestCase("buildTransitive")]
    [TestCase("buildMultiTargeting")]
    public void Covers_all_three_build_roots(string root)
    {
        var findings = Inspect(new SyntheticPackage().With($"{root}/Wrong.targets", "<Project />"));

        Assert.That(findings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Ignores_msbuild_files_outside_a_build_folder()
    {
        // tools/ and contentFiles/ may ship whatever they like; nothing imports them by convention.
        var findings = Inspect(new SyntheticPackage()
            .With("tools/net8.0/any/Something.targets", "<Project />")
            .With("contentFiles/any/any/Other.props", "<Project />"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Matches_the_package_id_without_regard_to_case()
    {
        var findings = Inspect(new SyntheticPackage().With("build/contoso.widgets.targets", "<Project />"));

        Assert.That(findings, Is.Empty);
    }
}
