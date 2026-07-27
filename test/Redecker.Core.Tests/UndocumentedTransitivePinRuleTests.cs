using System.Xml.Linq;
using NUnit.Framework;
using Redecker.Findings;
using Redecker.Projects;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class UndocumentedTransitivePinRuleTests
{
    private static List<Finding> Inspect(params (string File, string Xml)[] files)
    {
        var pins = new List<PackagePin>();
        foreach (var (file, xml) in files)
        {
            pins.AddRange(PinReader.Read(XDocument.Parse(xml, LoadOptions.SetLineInfo), file));
        }

        return new UndocumentedTransitivePinRule().Inspect(pins).ToList();
    }

    private const string Props =
        """
        <Project>
          <ItemGroup>
            <PackageVersion Include="Serilog" Version="3.1.1" />
            <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
          </ItemGroup>
        </Project>
        """;

    private const string ProjectReferencingSerilogOnly =
        """
        <Project>
          <ItemGroup>
            <PackageReference Include="Serilog" />
          </ItemGroup>
        </Project>
        """;

    [Test]
    public void Reports_a_version_that_no_project_references()
    {
        // Newtonsoft.Json is given a version but nothing asks for it: it is a floor someone
        // raised, or dead weight, and the file cannot say which.
        var findings = Inspect(
            ("Directory.Packages.props", Props),
            ("app.csproj", ProjectReferencingSerilogOnly));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Subject, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(findings[0].Title, Does.Contain("no project references it"));
        });
    }

    [Test]
    public void Is_silent_when_the_pin_carries_a_hint()
    {
        // The whole remedy. A documented floor is not a problem, it is the intended end state.
        var findings = Inspect(
            ("Directory.Packages.props",
                """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Serilog" Version="3.1.1" />
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3"
                                    Label="security-floor: #:package Newtonsoft.Json@13.0.3; until: transitive-floor(Serilog) >= 13.0.3" />
                  </ItemGroup>
                </Project>
                """),
            ("app.csproj", ProjectReferencingSerilogOnly));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Counts_a_reference_from_any_project_in_the_scan()
    {
        // A package referenced by one project among many is legitimately declared once centrally.
        var findings = Inspect(
            ("Directory.Packages.props", Props),
            ("a.csproj", ProjectReferencingSerilogOnly),
            ("b.csproj",
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Says_nothing_when_no_project_files_were_read()
    {
        // Checking Directory.Packages.props alone would otherwise report every entry in it,
        // which is noise rather than a finding.
        var findings = Inspect(("Directory.Packages.props", Props));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_each_package_once_however_many_files_declare_it()
    {
        var findings = Inspect(
            ("Directory.Packages.props", Props),
            ("more.props", Props),
            ("app.csproj", ProjectReferencingSerilogOnly));

        Assert.That(findings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Matches_package_ids_without_regard_to_case()
    {
        var findings = Inspect(
            ("Directory.Packages.props", Props),
            ("app.csproj",
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="serilog" />
                    <PackageReference Include="NEWTONSOFT.JSON" />
                  </ItemGroup>
                </Project>
                """));

        Assert.That(findings, Is.Empty);
    }
}
