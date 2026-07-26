using System.Xml.Linq;
using NUnit.Framework;
using Redecker.Hints;
using Redecker.Projects;

namespace Redecker.Tests;

[TestFixture]
public class PinReaderTests
{
    private static IReadOnlyList<PackagePin> Read(string xml) =>
        PinReader.Read(XDocument.Parse(xml, LoadOptions.SetLineInfo), "Directory.Packages.props");

    [Test]
    public void Reads_package_versions_and_their_hints()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.11"
                                Label="upstream-bug: #:package SQLitePCLRaw.bundle_e_sqlite3@2.1.11; until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12)" />
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(pins, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(pins[0].PackageId, Is.EqualTo("SQLitePCLRaw.bundle_e_sqlite3"));
            Assert.That(pins[0].Version, Is.EqualTo("2.1.11"));
            Assert.That(pins[0].Hint!.Kind, Is.EqualTo(HintKind.UpstreamBug));
            Assert.That(pins[1].Hint, Is.Null);
            Assert.That(pins[1].HintError, Is.Null);
        });
    }

    [Test]
    public void Inherits_a_label_from_the_containing_item_group()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup Label="framework-band: #:package Microsoft.Extensions.Logging; until: never">
                <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(pins[0].Hint!.Kind, Is.EqualTo(HintKind.FrameworkBand));
    }

    [Test]
    public void Captures_the_condition_that_carries_a_per_framework_pin()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(pins[0].Condition, Does.Contain("net8.0"));
    }

    [Test]
    public void Reads_package_references_and_version_child_elements()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="Serilog">
                  <Version>3.1.1</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        Assert.Multiple(() =>
        {
            Assert.That(pins[0].ItemType, Is.EqualTo("PackageReference"));
            Assert.That(pins[0].Version, Is.EqualTo("3.1.1"));
        });
    }

    [Test]
    public void Surfaces_a_malformed_hint_instead_of_dropping_it()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="X" Version="1.0.0" Label="security-floor: not-a-subject" />
              </ItemGroup>
            </Project>
            """);

        Assert.Multiple(() =>
        {
            Assert.That(pins[0].Hint, Is.Null);
            Assert.That(pins[0].HintError, Is.Not.Null);
        });
    }

    [Test]
    public void Records_line_numbers_so_findings_can_point_at_the_declaration()
    {
        var pins = Read(
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="X" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(pins[0].Line, Is.EqualTo(3));
    }
}
