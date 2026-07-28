using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class UntrackedOutputCopyRuleTests
{
    private static List<Finding> Inspect(string targets, string id = "Test.Package")
    {
        using var archive = new SyntheticPackage()
            .With($"build/net462/{id}.targets", targets)
            .Build(id, "1.0.0");
        return new UntrackedOutputCopyRule().Inspect(archive).ToList();
    }

    [Test]
    public void Reports_a_copy_into_the_output_directory_that_records_nothing()
    {
        // The shape Microsoft.Data.SqlClient.SNI ships for net462.
        var findings = Inspect(
            """
            <Project>
              <Target Name="CopyNativeFiles" AfterTargets="Build">
                <Copy SourceFiles="@(NativeFiles)"
                      DestinationFiles="@(NativeFiles -> '$(OutDir)%(Filename)%(Extension)')" />
              </Target>
            </Project>
            """);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Detail, Does.Contain("FileWrites"));
        });
    }

    [Test]
    public void Is_silent_when_the_copy_records_what_it_wrote()
    {
        var findings = Inspect(
            """
            <Project>
              <Target Name="CopyNativeFiles" AfterTargets="Build">
                <Copy SourceFiles="@(NativeFiles)" DestinationFolder="$(OutDir)">
                  <Output TaskParameter="CopiedFiles" ItemName="FileWrites" />
                </Copy>
              </Target>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Accepts_accounting_done_in_a_sibling_item_group()
    {
        // Less direct, but the files still end up in FileWrites.
        var findings = Inspect(
            """
            <Project>
              <Target Name="CopyNativeFiles" AfterTargets="Build">
                <Copy SourceFiles="@(NativeFiles)" DestinationFolder="$(OutDir)" />
                <ItemGroup>
                  <FileWrites Include="@(NativeFiles -> '$(OutDir)%(Filename)%(Extension)')" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_copies_that_do_not_target_build_output()
    {
        // Staging into obj/ or a custom folder is not IncrementalClean's business.
        var findings = Inspect(
            """
            <Project>
              <Target Name="Stage">
                <Copy SourceFiles="@(Things)" DestinationFolder="$(IntermediateOutputPath)staging" />
              </Target>
            </Project>
            """);

        Assert.That(findings, Is.Empty);
    }

    [TestCase("$(OutDir)")]
    [TestCase("$(OutputPath)")]
    [TestCase("$(TargetDir)")]
    [TestCase("$(PublishDir)")]
    public void Recognises_each_output_location(string property)
    {
        var findings = Inspect(
            $"""
             <Project>
               <Target Name="T">
                 <Copy SourceFiles="a.dll" DestinationFolder="{property}" />
               </Target>
             </Project>
             """);

        Assert.That(findings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Notes_a_hand_rolled_delete_as_corroboration()
    {
        // Shipping your own Clean is only necessary because MSBuild was never told about the
        // files, so it is evidence for the finding rather than a mitigation of it.
        var findings = Inspect(
            """
            <Project>
              <Target Name="CopyNativeFiles">
                <Copy SourceFiles="@(N)" DestinationFolder="$(OutDir)" />
              </Target>
              <Target Name="CleanNativeFiles">
                <Delete Files="@(N -> '$(OutDir)%(Filename)%(Extension)')" />
              </Target>
            </Project>
            """);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Detail, Does.Contain("giveaway"));
    }

    [Test]
    public void Handles_the_legacy_msbuild_xml_namespace()
    {
        // Packages targeting net4x commonly still use the 2003 namespace, as SNI does.
        var findings = Inspect(
            """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Target Name="CopyNativeFiles">
                <Copy SourceFiles="@(N)" DestinationFolder="$(OutDir)" />
              </Target>
            </Project>
            """);

        Assert.That(findings, Has.Count.EqualTo(1));
    }
}
