using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class FrameworkFolderMismatchRuleTests
{
    private static List<Finding> Inspect(string entry, byte[] assembly, string id = "Contoso.Widgets")
    {
        using var archive = new SyntheticPackage().With(entry, assembly).Build(id);
        return new FrameworkFolderMismatchRule().Inspect(archive).ToList();
    }

    // Every one of these was observed in the corpus survey and works. Folder and assembly
    // differing is not the defect; a consumer being unable to load the assembly is.
    [TestCase("lib/net8.0/Contoso.Widgets.dll", ".NETStandard,Version=v2.0",
        TestName = "netstandard2.0 under net8.0, the usual way to win nearest-framework matching")]
    [TestCase("lib/netcoreapp3.1/Contoso.Widgets.dll", ".NETStandard,Version=v2.0",
        TestName = "netstandard2.0 under netcoreapp3.1")]
    [TestCase("lib/netstandard2.1/Contoso.Widgets.dll", ".NETStandard,Version=v2.0",
        TestName = "netstandard2.0 under netstandard2.1")]
    [TestCase("lib/net452/Contoso.Widgets.dll", ".NETFramework,Version=v4.5",
        TestName = "net45 build reused under net452")]
    [TestCase("lib/net472/Contoso.Widgets.dll", ".NETStandard,Version=v2.0",
        TestName = "netstandard2.0 under net472, above the 4.6.1 floor")]
    [TestCase("lib/net8.0/Contoso.Widgets.dll", ".NETCoreApp,Version=v8.0",
        TestName = "exact match")]
    public void Stays_silent_when_a_consumer_could_load_it(string entry, string declared)
    {
        Assert.That(Inspect(entry, CompiledAssembly.Targeting(declared)), Is.Empty);
    }

    // Each of these is a real package from the survey, reduced to the pairing that made it fail.
    [TestCase("lib/net45/Contoso.Widgets.dll", ".NETFramework,Version=v4.7.2",
        TestName = "Microsoft.VisualStudio.TextTemplating.15.0: net472 assembly under net45")]
    [TestCase("lib/net461/Contoso.Widgets.dll", ".NETFramework,Version=v4.7",
        TestName = "System.Security.Cryptography.OpenSsl: net47 assembly under net461")]
    [TestCase("lib/netstandard1.5/Contoso.Widgets.dll", ".NETFramework,Version=v4.5",
        TestName = "Microsoft.Web.Administration: .NET Framework assembly under netstandard")]
    [TestCase("lib/net45/Contoso.Widgets.dll", ".NETStandard,Version=v2.0",
        TestName = "DesignTools.Extensibility: netstandard2.0 below its net461 floor")]
    [TestCase("lib/net8.0/Contoso.Widgets.dll", ".NETFramework,Version=v4.0",
        TestName = "Microsoft.CodeCoverage: .NET Framework shim under net8.0")]
    public void Reports_an_assembly_a_consumer_could_not_load(string entry, string declared)
    {
        var findings = Inspect(entry, CompiledAssembly.Targeting(declared));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Code, Is.EqualTo("RDK0010"));
        Assert.That(findings[0].Title, Does.Contain(declared));
    }

    [Test]
    public void Is_a_warning_because_shipped_packages_do_this_deliberately()
    {
        var findings = Inspect(
            "lib/net8.0/Contoso.Widgets.dll", CompiledAssembly.Targeting(".NETFramework,Version=v4.0"));

        Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
    }

    [Test]
    public void Says_whose_assembly_it_is()
    {
        var own = Inspect(
            "lib/net45/Contoso.Widgets.dll", CompiledAssembly.Targeting(".NETFramework,Version=v4.7.2"));
        var bundled = Inspect(
            "lib/net45/Newtonsoft.Json.dll", CompiledAssembly.Targeting(".NETFramework,Version=v4.7.2"));

        Assert.That(own[0].Detail, Does.Contain("the package's own assembly"));
        Assert.That(bundled[0].Detail, Does.Contain("a bundled dependency"));
    }

    // Dead platforms have version schemes that are each their own private joke, and nobody can
    // republish those packages anyway. Half of every incompatible pairing in the survey was here.
    [TestCase("lib/uap10.0/Contoso.Widgets.dll", ".NETStandard,Version=v2.0", TestName = "UAP")]
    [TestCase("lib/MonoAndroid403/Contoso.Widgets.dll", ".NETStandard,Version=v2.0", TestName = "MonoAndroid")]
    [TestCase("lib/xamarinios10/Contoso.Widgets.dll", ".NETStandard,Version=v2.0", TestName = "Xamarin")]
    [TestCase("lib/sl5/Contoso.Widgets.dll", ".NETFramework,Version=v4.5", TestName = "Silverlight")]
    [TestCase("lib/netcore45/Contoso.Widgets.dll", ".NETFramework,Version=v4.5", TestName = "Windows Store")]
    [TestCase("lib/portable-net45+win8/Contoso.Widgets.dll", ".NETFramework,Version=v4.5", TestName = "PCL")]
    [TestCase("lib/tizen80/Contoso.Widgets.dll", ".NETFramework,Version=v4.5", TestName = "Tizen")]
    public void Ignores_frameworks_nobody_ships_to(string entry, string declared)
    {
        Assert.That(Inspect(entry, CompiledAssembly.Targeting(declared)), Is.Empty);
    }

    [Test]
    public void Ignores_an_assembly_with_no_target_framework_attribute()
    {
        // 379 of the 9,221 assemblies surveyed carry none. Guessing would be worse than silence.
        Assert.That(
            Inspect("lib/net45/Contoso.Widgets.dll", CompiledAssembly.WithNoTargetFramework()),
            Is.Empty);
    }

    [Test]
    public void Ignores_a_file_that_is_not_a_managed_assembly()
    {
        Assert.That(
            Inspect("lib/net45/Contoso.Widgets.dll", "MZ but not really"u8.ToArray()),
            Is.Empty);
    }

    [Test]
    public void Ignores_satellites_and_anything_outside_a_framework_folder()
    {
        var assembly = CompiledAssembly.Targeting(".NETFramework,Version=v4.7.2");

        Assert.Multiple(() =>
        {
            Assert.That(
                Inspect("lib/net45/de/Contoso.Widgets.resources.dll", assembly), Is.Empty,
                "a satellite has no framework of its own");
            Assert.That(
                Inspect("tools/net45/Contoso.Widgets.dll", assembly), Is.Empty,
                "a tool payload is not compiled against by anyone");
            Assert.That(
                Inspect("lib/Contoso.Widgets.dll", assembly), Is.Empty,
                "no folder means no promise to break");
        });
    }

    [Test]
    public void Groups_every_assembly_that_shares_a_folder_and_a_target()
    {
        var assembly = CompiledAssembly.Targeting(".NETFramework,Version=v4.7.2");
        using var archive = new SyntheticPackage()
            .With("lib/net45/Contoso.Widgets.dll", assembly)
            .With("lib/net45/Contoso.Widgets.Core.dll", assembly)
            .With("lib/net45/Contoso.Widgets.Data.dll", assembly)
            .Build("Contoso.Widgets");

        var findings = new FrameworkFolderMismatchRule().Inspect(archive).ToList();

        Assert.That(findings, Has.Count.EqualTo(1), "one finding per folder and target, not per file");
        Assert.That(findings[0].Detail, Does.Contain("lib/net45/Contoso.Widgets.Core.dll"));
        Assert.That(findings[0].Detail, Does.Contain(" declare "), "plural, because there are three");
    }

    [Test]
    public void Is_reviewable_by_name()
    {
        var rule = new FrameworkFolderMismatchRule();

        Assert.Multiple(() =>
        {
            Assert.That(rule.Code, Is.EqualTo("RDK0010"));
            Assert.That(rule.Name, Is.EqualTo("assembly does not match its framework folder"));
        });
    }
}
