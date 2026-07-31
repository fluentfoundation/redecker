using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class AnalyzerPlacementRuleTests
{
    private static List<Finding> Inspect(params string[] entries)
    {
        var package = new SyntheticPackage();
        foreach (var entry in entries)
        {
            package = package.With(entry);
        }

        using var archive = package.Build("Contoso.Analyzers", "1.0.0");
        return new AnalyzerPlacementRule().Inspect(archive).ToList();
    }

    // Every layout below was observed in the top 500 packages on nuget.org and works. The rule
    // exists to catch a different mistake, and must stay silent on all of them.
    [TestCase("analyzers/dotnet/cs/Contoso.Analyzers.dll", TestName = "dotnet/cs")]
    [TestCase("analyzers/dotnet/vb/Contoso.Analyzers.dll", TestName = "dotnet/vb")]
    [TestCase("analyzers/cs/Contoso.Analyzers.dll", TestName = "older analyzers/cs")]
    [TestCase("analyzers/vb/Contoso.Analyzers.dll", TestName = "older analyzers/vb")]
    [TestCase("analyzers/dotnet/Contoso.Analyzers.dll", TestName = "language-agnostic")]
    [TestCase("analyzers/dotnet/roslyn4.8/cs/Contoso.Analyzers.dll", TestName = "roslyn-versioned")]
    [TestCase("analyzers/dotnet/cs/de/Contoso.Analyzers.resources.dll", TestName = "localised")]
    [TestCase("analyzers/dotnet/cs/zh-Hans/Contoso.Analyzers.resources.dll", TestName = "localised, script tag")]
    [TestCase("analyzers/dotnet/cs/cs/Contoso.Analyzers.resources.dll", TestName = "Czech under C#")]
    [TestCase("analyzers/dotnet/roslyn4.4/cs/pt-BR/Contoso.Analyzers.resources.dll", TestName = "versioned and localised")]
    public void Accepts_every_layout_seen_in_the_wild(string entry)
    {
        Assert.That(Inspect(entry), Is.Empty);
    }

    [TestCase("analyzers/net8.0/Contoso.Analyzers.dll", "net8.0")]
    [TestCase("analyzers/net472/Contoso.Analyzers.dll", "net472")]
    [TestCase("analyzers/dotnet/net8.0/Contoso.Analyzers.dll", "net8.0")]
    [TestCase("analyzers/netstandard2.0/cs/Contoso.Analyzers.dll", "netstandard2.0")]
    public void Reports_the_lib_layout_applied_to_analyzers(string entry, string expected)
    {
        var findings = Inspect(entry);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Title, Does.Contain(expected));
        });
    }

    [Test]
    public void Groups_by_framework_rather_than_reporting_every_file()
    {
        var findings = Inspect(
            "analyzers/net8.0/One.dll",
            "analyzers/net8.0/Two.dll",
            "analyzers/net8.0/Three.dll");

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Detail, Does.Contain("analyzers/net8.0/One.dll"));
    }

    [Test]
    public void Says_nothing_about_a_package_with_no_analyzers()
    {
        Assert.That(Inspect("lib/net8.0/Contoso.dll", "build/net8.0/Contoso.targets"), Is.Empty);
    }

    [Test]
    public void Ignores_non_assemblies_under_analyzers()
    {
        // A readme or a resource file in the wrong place is not a silently inert analyzer.
        Assert.That(Inspect("analyzers/net8.0/notes.txt"), Is.Empty);
    }

    [Test]
    public void Carries_a_reviewable_name()
    {
        Assert.That(new AnalyzerPlacementRule().Name, Is.EqualTo("analyzer under a framework folder"));
    }
}
