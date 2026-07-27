using System.Xml.Linq;
using NUnit.Framework;
using Redecker.Findings;
using Redecker.Frameworks;
using Redecker.Projects;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class LockstepFamilyRuleTests
{
    private static List<Finding> Inspect(string xml, BandPolicy? policy = null) =>
        new LockstepFamilyRule(policy)
            .Inspect(PinReader.Read(XDocument.Parse(xml, LoadOptions.SetLineInfo), "Directory.Packages.props"))
            .ToList();

    [Test]
    public void Reports_an_ef_core_family_split_across_versions()
    {
        // Exactly what an automatic updater produces: it bumps whichever members happen to have
        // a newer release and splits the set, and restore still succeeds.
        var findings = Inspect(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.5" />
                <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
                <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.5" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Error));
            Assert.That(findings[0].Title, Does.Contain("9.0.0"));
            Assert.That(findings[0].Title, Does.Contain("9.0.5"));
            Assert.That(findings[0].Detail, Does.Contain("Microsoft.EntityFrameworkCore.SqlServer 9.0.0"));
        });
    }

    [Test]
    public void Is_silent_when_the_whole_family_agrees()
    {
        var findings = Inspect(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.5" />
                <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.5" />
                <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.5" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_packages_outside_a_lockstep_family()
    {
        // Differing versions are entirely normal for unrelated packages.
        var findings = Inspect(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageVersion Include="Serilog" Version="3.1.1" />
                <PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_a_reference_whose_version_comes_from_central_management()
    {
        // A bare PackageReference carries no version; the PackageVersion item holds the
        // constraint, so reporting this one would be a false positive.
        var findings = Inspect(
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
                <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.5" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Honours_a_custom_lockstep_family()
    {
        var policy = new BandPolicy(lockstepPrefixes: ["Contoso.Data"]);

        var findings = Inspect(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Contoso.Data.Core" Version="1.0.0" />
                <PackageVersion Include="Contoso.Data.SqlServer" Version="1.1.0" />
              </ItemGroup>
            </Project>
            """,
            policy);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Subject, Is.EqualTo("Contoso.Data*"));
    }
}
