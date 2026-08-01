using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class PrereleaseDependencyRuleTests
{
    private static List<Finding> Inspect(string packageVersion, params string[] dependencies)
    {
        var items = string.Join(
            "",
            dependencies.Chunk(2).Select(p => $"""<dependency id="{p[0]}" version="{p[1]}" />"""));

        using var archive = new SyntheticPackage()
            .With("Contoso.Widgets.nuspec", $"""
                <?xml version="1.0"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata>
                    <id>Contoso.Widgets</id>
                    <version>{packageVersion}</version>
                    <dependencies><group targetFramework="net8.0">{items}</group></dependencies>
                  </metadata>
                </package>
                """)
            .Build("Contoso.Widgets", packageVersion);

        return new PrereleaseDependencyRule().Inspect(archive).ToList();
    }

    // Reduced from Microsoft.Azure.Workflows.WebJobs.Extension@1.44.16, which ships stable and
    // depends on Microsoft.Azure.WebJobs.Script.Abstractions 1.0.0-preview.
    [Test]
    public void Reports_a_stable_package_depending_on_a_prerelease()
    {
        var findings = Inspect("1.44.16", "Contoso.Abstractions", "1.0.0-preview");

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Code, Is.EqualTo("RDK0012"));
        Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
        Assert.That(findings[0].Title, Does.Contain("Contoso.Abstractions 1.0.0-preview"));
    }

    [TestCase("[1.0.0-beta, )", TestName = "explicit range with a prerelease floor")]
    [TestCase("1.0.0-rc.1", TestName = "bare prerelease, meaning a minimum")]
    [TestCase("[2.0.0-preview.3, 3.0.0)", TestName = "bounded, prerelease floor")]
    public void Recognises_a_prerelease_lower_bound_in_any_form(string range)
    {
        Assert.That(Inspect("1.0.0", "Contoso.Abstractions", range), Has.Count.EqualTo(1));
    }

    // The first pass at this matched a pattern against the version string, and would have called
    // this a finding. Restore resolves it to a stable version.
    [Test]
    public void Ignores_a_prerelease_upper_bound()
    {
        Assert.That(Inspect("1.0.0", "Contoso.Abstractions", "[1.0.0, 2.0.0-preview)"), Is.Empty);
    }

    [Test]
    public void Ignores_a_prerelease_package_depending_on_prereleases()
    {
        // Entirely ordinary, and says nothing about anybody's stability.
        Assert.That(Inspect("2.0.0-beta.1", "Contoso.Abstractions", "1.0.0-preview"), Is.Empty);
    }

    [Test]
    public void Stays_silent_on_stable_dependencies()
    {
        Assert.That(
            Inspect("1.0.0", "Contoso.Abstractions", "1.0.0", "Contoso.Core", "[2.0.0, 3.0.0)"),
            Is.Empty);
    }

    [Test]
    public void Reports_every_offender_in_one_finding()
    {
        var findings = Inspect(
            "1.0.0",
            "Contoso.A", "1.0.0-preview",
            "Contoso.B", "2.0.0-beta",
            "Contoso.C", "3.0.0",
            "Contoso.D", "4.0.0-rc.1",
            "Contoso.E", "5.0.0-alpha");

        Assert.That(findings, Has.Count.EqualTo(1), "one finding per package, not per dependency");
        Assert.That(findings[0].Title, Does.Contain("4 prereleases"));
        Assert.That(findings[0].Detail, Does.Contain("and 1 more"));
        Assert.That(findings[0].Detail, Does.Not.Contain("Contoso.C"), "the stable one is not listed");
    }

    [Test]
    public void Says_nothing_about_a_package_with_no_dependencies()
    {
        Assert.That(Inspect("1.0.0"), Is.Empty);
    }

    [Test]
    public void Is_reviewable_by_name()
    {
        var rule = new PrereleaseDependencyRule();

        Assert.Multiple(() =>
        {
            Assert.That(rule.Code, Is.EqualTo("RDK0012"));
            Assert.That(rule.Name, Is.EqualTo("stable package depends on a prerelease"));
        });
    }
}
