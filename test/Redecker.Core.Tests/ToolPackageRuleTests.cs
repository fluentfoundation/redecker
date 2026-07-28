using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class ToolPackageRuleTests
{
    private const string ToolNuspec =
        """
        <package><metadata>
          <id>dotnet-thing</id><version>1.0.0</version>
          <packageTypes><packageType name="DotnetTool" /></packageTypes>
        </metadata></package>
        """;

    private const string Settings =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <DotNetCliTool Version="1">
          <Commands><Command Name="thing" EntryPoint="dotnet-thing.dll" Runner="dotnet" /></Commands>
        </DotNetCliTool>
        """;

    private static List<Finding> Inspect(SyntheticPackage p, string id = "dotnet-thing")
    {
        using var archive = p.Build(id, "1.0.0");
        return new ToolPackageRule().Inspect(archive).ToList();
    }

    [Test]
    public void Says_nothing_about_a_package_that_is_not_a_tool()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("X.nuspec", "<package><metadata><id>X</id><version>1.0.0</version></metadata></package>")
            .With("lib/net8.0/X.dll"), "X");

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Accepts_a_correctly_built_tool()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/DotnetToolSettings.xml", Settings)
            .With("tools/net8.0/any/dotnet-thing.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_the_missing_settings_file()
    {
        // The failure six separate repositories have reported: installs fail for everyone, on a
        // version that cannot be unpublished.
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/dotnet-thing.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Error));
            Assert.That(findings[0].Title, Does.Contain("DotnetToolSettings.xml"));
        });
    }

    [Test]
    public void Reports_every_framework_folder_that_is_missing_one()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/DotnetToolSettings.xml", Settings)
            .With("tools/net8.0/any/dotnet-thing.dll")
            .With("tools/net9.0/any/dotnet-thing.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("net9.0"));
    }

    [Test]
    public void Reports_a_tool_with_no_tools_folder_at_all()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("lib/net8.0/dotnet-thing.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("ships no tools/"));
    }

    [Test]
    public void Reports_an_entry_point_that_is_not_in_the_package()
    {
        // Installs fine, then fails to run: worse than failing to install.
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/DotnetToolSettings.xml", Settings));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("dotnet-thing.dll"));
    }

    [Test]
    public void Reports_settings_with_no_entry_point()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/DotnetToolSettings.xml",
                "<DotNetCliTool Version=\"1\"><Commands><Command Name=\"thing\" /></Commands></DotNetCliTool>")
            .With("tools/net8.0/any/dotnet-thing.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("no EntryPoint"));
    }

    [Test]
    public void Warns_when_the_command_name_looks_like_an_assembly()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("dotnet-thing.nuspec", ToolNuspec)
            .With("tools/net8.0/any/DotnetToolSettings.xml",
                "<DotNetCliTool Version=\"1\"><Commands>" +
                "<Command Name=\"dotnet-thing.dll\" EntryPoint=\"dotnet-thing.dll\" Runner=\"dotnet\" />" +
                "</Commands></DotNetCliTool>")
            .With("tools/net8.0/any/dotnet-thing.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
    }
}
