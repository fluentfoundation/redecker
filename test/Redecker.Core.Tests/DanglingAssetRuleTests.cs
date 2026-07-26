using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class DanglingAssetRuleTests
{
    private static List<Finding> Inspect(SyntheticPackage package)
    {
        using var archive = package.Build();
        return new DanglingAssetRule().Inspect(archive).ToList();
    }

    [Test]
    public void Reports_a_referenced_file_the_package_does_not_ship()
    {
        var findings = Inspect(new SyntheticPackage()
            .WithCopyTargets("buildTransitive/net461/Test.targets", @"..\..\runtimes\win-arm\native\e_sqlite3.dll")
            .With("runtimes/win-x64/native/e_sqlite3.dll"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Error));
            Assert.That(findings[0].Title, Does.Contain("runtimes/win-arm/native/e_sqlite3.dll"));
        });
    }

    [Test]
    public void Is_silent_when_every_referenced_file_is_present()
    {
        var findings = Inspect(new SyntheticPackage()
            .WithCopyTargets("buildTransitive/net461/Test.targets", @"..\..\runtimes\win-x64\native\e_sqlite3.dll")
            .With("runtimes/win-x64/native/e_sqlite3.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Resolves_forward_and_back_slashes_alike()
    {
        var findings = Inspect(new SyntheticPackage()
            .WithCopyTargets("build/net8.0/Test.targets", "../../lib/net8.0/Test.dll")
            .With("lib/net8.0/Test.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_references_it_cannot_resolve_with_certainty()
    {
        // Each of these depends on evaluation context the rule does not have. Reporting them
        // would make the rule unusable as a gate, so silence is the correct behaviour.
        var findings = Inspect(new SyntheticPackage()
            .WithCopyTargets(
                "build/net8.0/Test.targets",
                @"..\..\runtimes\$(RuntimeIdentifier)\native\x.dll",
                @"..\..\runtimes\**\*.dll",
                @"..\..\lib\%(Identity)\x.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_paths_that_are_not_anchored_to_the_package()
    {
        // Without $(MSBuildThisFileDirectory) the path is relative to the consuming project.
        var findings = Inspect(new SyntheticPackage()
            .With("build/net8.0/Test.targets",
                """
                <Project>
                  <ItemGroup>
                    <None Include="somewhere/else/x.dll" />
                  </ItemGroup>
                </Project>
                """));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Does_not_treat_conditions_as_paths()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/net8.0/Test.targets",
                """
                <Project>
                  <ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)../../lib/net8.0/Test.dll')">
                    <None Include="$(MSBuildThisFileDirectory)../../lib/net8.0/Test.dll" />
                  </ItemGroup>
                </Project>
                """)
            .With("lib/net8.0/Test.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Only_reads_msbuild_files_that_consumers_import()
    {
        // A targets file under tools/ is not imported by consumers, so its references are not
        // this rule's business.
        var findings = Inspect(new SyntheticPackage()
            .WithCopyTargets("tools/Test.targets", "../missing.dll"));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_unparseable_msbuild_files_as_info_rather_than_failing()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/net8.0/Test.targets", "<Project><ItemGroup></Project>"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Info));
    }

    [TestCase(@"..\..\runtimes\win-arm\native\e_sqlite3.dll", "buildTransitive/net461/", "runtimes/win-arm/native/e_sqlite3.dll")]
    [TestCase("../lib/net8.0/X.dll", "build/", "lib/net8.0/X.dll")]
    [TestCase("./X.props", "build/net8.0/", "build/net8.0/X.props")]
    public void Resolves_relative_paths_against_the_msbuild_file(string reference, string directory, string expected)
    {
        var resolved = DanglingAssetRule.TryResolve(
            "$(MSBuildThisFileDirectory)" + reference, directory, out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public void Refuses_paths_that_escape_the_package_root()
    {
        var resolved = DanglingAssetRule.TryResolve(
            @"$(MSBuildThisFileDirectory)..\..\..\..\outside.dll", "build/", out _);

        Assert.That(resolved, Is.False);
    }
}
