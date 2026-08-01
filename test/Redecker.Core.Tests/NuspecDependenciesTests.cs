using NuGet.Versioning;
using NUnit.Framework;
using Redecker.Packages;

namespace Redecker.Tests;

[TestFixture]
public class NuspecDependenciesTests
{
    private static string Nuspec(string dependencies, string? xmlns =
        " xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"") => $"""
        <?xml version="1.0"?>
        <package{xmlns}>
          <metadata>
            <id>Contoso.Provider</id>
            <version>1.0.0</version>
            <dependencies>{dependencies}</dependencies>
          </metadata>
        </package>
        """;

    [Test]
    public void Reads_dependencies_inside_a_framework_group()
    {
        var read = NuspecDependencies.Read(Nuspec(
            """<group targetFramework="net8.0"><dependency id="Contoso.Core" version="[9.0.0, 10.0.0)" /></group>"""));

        Assert.That(read, Has.Count.EqualTo(1));
        Assert.That(read[0].Id, Is.EqualTo("Contoso.Core"));
        Assert.That(read[0].TargetFramework, Is.EqualTo("net8.0"));
        Assert.That(read[0].Range.Satisfies(new NuGetVersion(9, 5, 0)), Is.True);
        Assert.That(read[0].Range.Satisfies(new NuGetVersion(10, 0, 0)), Is.False);
    }

    [Test]
    public void Reads_a_flat_dependency_outside_any_group()
    {
        // Older nuspecs declare dependencies with no group at all.
        var read = NuspecDependencies.Read(
            Nuspec("""<dependency id="Contoso.Core" version="1.0.0" />"""));

        Assert.That(read, Has.Count.EqualTo(1));
        Assert.That(read[0].TargetFramework, Is.Null);
    }

    [Test]
    public void Reads_every_group_separately()
    {
        // NuGet applies only the group matching the consuming project, so both must survive
        // parsing for a caller to decide which one applies.
        var read = NuspecDependencies.Read(Nuspec("""
            <group targetFramework="net8.0"><dependency id="Contoso.Core" version="[8.0.0, 9.0.0)" /></group>
            <group targetFramework="net10.0"><dependency id="Contoso.Core" version="[10.0.0, 11.0.0)" /></group>
            """));

        Assert.That(read, Has.Count.EqualTo(2));
        Assert.That(
            read.Any(d => d.Range.Satisfies(new NuGetVersion(10, 0, 0))), Is.True,
            "one group admits 10.0.0 even though the other does not");
    }

    // The reason this is XML rather than a regular expression: each of these would need its own
    // pattern, and getting one wrong would be silent.
    [Test]
    public void Survives_the_shapes_a_regex_would_miss()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                NuspecDependencies.Read(Nuspec(
                    """<dependency version="1.0.0" id="Contoso.Core" />""")),
                Has.Count.EqualTo(1), "attributes in the other order");

            Assert.That(
                NuspecDependencies.Read(Nuspec(
                    "<dependency\n  id='Contoso.Core'\n  version='1.0.0' />")),
                Has.Count.EqualTo(1), "single quotes and newlines inside the tag");

            Assert.That(
                NuspecDependencies.Read(Nuspec(
                    """<dependency id="Contoso.Core" version="1.0.0" />""", xmlns: "")),
                Has.Count.EqualTo(1), "no namespace at all");

            Assert.That(
                NuspecDependencies.Read(Nuspec(
                    """<dependency id="Contoso.Core" version="1.0.0" exclude="Build,Analyzers" />""")),
                Has.Count.EqualTo(1), "extra attributes");
        });
    }

    [Test]
    public void Skips_a_dependency_with_no_version()
    {
        // "Any version" constrains nothing, so there is no range to test a pin against.
        Assert.That(
            NuspecDependencies.Read(Nuspec("""<dependency id="Contoso.Core" />""")),
            Is.Empty);
    }

    [TestCase(null, TestName = "null")]
    [TestCase("", TestName = "empty")]
    [TestCase("   ", TestName = "whitespace")]
    [TestCase("<package><metadata>", TestName = "truncated, unparsable XML")]
    [TestCase("this is not xml", TestName = "not XML at all")]
    public void Returns_nothing_rather_than_throwing_on_junk(string? nuspec)
    {
        Assert.That(NuspecDependencies.Read(nuspec), Is.Empty);
    }

    [Test]
    public void Skips_a_version_that_is_not_a_range()
    {
        Assert.That(
            NuspecDependencies.Read(Nuspec("""<dependency id="Contoso.Core" version="not-a-version" />""")),
            Is.Empty);
    }
}
