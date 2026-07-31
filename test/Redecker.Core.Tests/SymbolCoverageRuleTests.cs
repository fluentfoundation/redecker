using NUnit.Framework;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class SymbolCoverageRuleTests
{
    private static List<Finding> Inspect(SyntheticPackage package, SyntheticPackage? symbols)
    {
        using var p = package.Build("Contoso.Widgets", "1.0.0");
        using PackageArchive? s = symbols?.Build("Contoso.Widgets", "1.0.0");
        return new SymbolCoverageRule().Inspect(p, s).ToList();
    }

    [Test]
    public void Says_nothing_when_no_symbol_package_was_published()
    {
        // 174 of 232 sampled packages ship no symbols. That is a choice, not a defect, and this
        // rule has no opinion about it.
        var findings = Inspect(new SyntheticPackage().With("lib/net8.0/Contoso.Widgets.dll"), null);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Says_nothing_when_every_assembly_is_covered()
    {
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.dll")
                .With("lib/net472/Contoso.Widgets.dll"),
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.pdb")
                .With("lib/net472/Contoso.Widgets.pdb"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_a_framework_the_symbol_package_missed()
    {
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.dll")
                .With("lib/net472/Contoso.Widgets.dll"),
            new SyntheticPackage().With("lib/net8.0/Contoso.Widgets.pdb"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Title, Does.Contain("1 of 2"));
            Assert.That(findings[0].Detail, Does.Contain("lib/net472/Contoso.Widgets.dll"));
        });
    }

    [Test]
    public void Ignores_satellite_assemblies_in_locale_folders()
    {
        // Microsoft.VisualStudio.Validation ships 26 of these and no PDBs for them, correctly:
        // a resource assembly has no code to step through.
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.dll")
                .With("lib/net8.0/de/Contoso.Widgets.resources.dll")
                .With("lib/net8.0/zh-Hans/Contoso.Widgets.resources.dll"),
            new SyntheticPackage().With("lib/net8.0/Contoso.Widgets.pdb"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_a_satellite_assembly_even_when_it_is_not_in_a_locale_folder()
    {
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.dll")
                .With("lib/net8.0/Contoso.Widgets.resources.dll"),
            new SyntheticPackage().With("lib/net8.0/Contoso.Widgets.pdb"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_assemblies_outside_lib()
    {
        // A tool package bundles its dependencies; third-party assemblies will never have PDBs
        // and reporting them would make the rule useless on every tool package.
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/Contoso.Widgets.dll")
                .With("tools/net8.0/any/Newtonsoft.Json.dll")
                .With("runtimes/win-x64/native/native.dll"),
            new SyntheticPackage().With("lib/net8.0/Contoso.Widgets.pdb"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Counts_every_uncovered_assembly_in_the_summary()
    {
        var findings = Inspect(
            new SyntheticPackage()
                .With("lib/net8.0/A.dll")
                .With("lib/net8.0/B.dll")
                .With("lib/net8.0/C.dll"),
            new SyntheticPackage().With("lib/net8.0/A.pdb"));

        Assert.That(findings[0].Title, Does.Contain("1 of 3"));
    }

    [Test]
    public void Carries_a_reviewable_name()
    {
        Assert.That(new SymbolCoverageRule().Name, Is.EqualTo("incomplete symbol package"));
    }
}
