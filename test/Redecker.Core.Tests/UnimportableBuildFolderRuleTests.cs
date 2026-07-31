using NUnit.Framework;
using Redecker.Findings;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class UnimportableBuildFolderRuleTests
{
    private static List<Finding> Inspect(SyntheticPackage p, string id = "Contoso.Widgets")
    {
        using var archive = p.Build(id, "1.0.0");
        return new UnimportableBuildFolderRule().Inspect(archive).ToList();
    }

    private const string Empty = "<Project />";

    private static string Importing(params string[] projects) =>
        "<Project>" +
        string.Join("", projects.Select(p => $"<Import Project=\"$(MSBuildThisFileDirectory){p}\" />")) +
        "</Project>";

    [Test]
    public void Accepts_files_named_after_the_package()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.props", Empty)
            .With("build/Contoso.Widgets.targets", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_a_package_with_no_entry_point_anywhere()
    {
        var findings = Inspect(new SyntheticPackage().With("build/Common.targets", Empty));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            // A warning, not an error: the file may be imported by the SDK, a workload, or a
            // consumer, none of which is visible from the package.
            Assert.That(findings[0].Severity, Is.EqualTo(FindingSeverity.Warning));
            Assert.That(findings[0].Detail, Does.Contain("Common.targets"));
        });
    }

    [Test]
    public void Names_the_benign_explanation_in_the_finding()
    {
        // A warning that pretends to be certain is worse than one that says what to check.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets", Empty)
            .With("build/Orphan.targets", Empty));

        Assert.That(findings[0].Detail, Does.Contain("SDK"));
    }

    [Test]
    public void Accepts_a_helper_imported_from_the_entry_point()
    {
        // Grpc.Tools: build/Grpc.Tools.props imports _grpc/ and _protobuf/.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.props", Importing("_helpers/Internal.props"))
            .With("build/_helpers/Internal.props", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Accepts_a_root_helper_imported_from_a_framework_folder()
    {
        // Win2D: build/win10/Microsoft.Graphics.Win2D.targets imports ..\Win2D.common.targets.
        // The inverse arrangement of the case above, and equally legitimate.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Common.targets", Empty)
            .With("build/win10/Contoso.Widgets.targets", Importing("../Common.targets")));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Reports_a_file_that_nothing_imports()
    {
        // Microsoft.Azure.StreamAnalytics.CICD: build/StreamAnalytics.targets imports only a
        // framework file, and nothing imports it.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets", Empty)
            .With("build/Orphan.targets", Empty));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("build/Orphan.targets"));
    }

    [Test]
    public void Follows_imports_transitively()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets", Importing("a/First.targets"))
            .With("build/a/First.targets", Importing("../b/Second.targets"))
            .With("build/b/Second.targets", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Says_nothing_when_an_import_is_held_entirely_in_a_property()
    {
        // CommunityToolkit.Mvvm and Nuke.Common both do this. The path is unresolvable here, so
        // it could reach anything, and nothing may be called unreachable afterwards.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets",
                "<Project><Import Project=\"$(_ContosoExtraTargets)\" /></Project>")
            .With("build/Contoso.Widgets.Extra.targets", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Accepts_a_file_handed_to_msbuild_through_an_extension_point_property()
    {
        // Verify sets CustomAfterMicrosoftCommonProps rather than importing directly. Naming the
        // file in reachable build logic is enough to establish intent.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.props",
                "<Project><PropertyGroup><CustomAfterMicrosoftCommonProps>" +
                "$(MSBuildThisFileDirectory)Contoso.Widgets.AfterSdk.props" +
                "</CustomAfterMicrosoftCommonProps></PropertyGroup></Project>")
            .With("build/Contoso.Widgets.AfterSdk.props", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Says_nothing_when_an_import_path_cannot_be_resolved()
    {
        // A computed import could reach anything, so claiming a file is unreachable would be a
        // guess. Staying quiet costs a true positive; guessing costs the rule's credibility.
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets",
                "<Project><Import Project=\"$(MSBuildThisFileDirectory)$(Flavour)/Thing.targets\" /></Project>")
            .With("build/Orphan.targets", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Ignores_imports_of_framework_files_outside_the_package()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("build/Contoso.Widgets.targets",
                "<Project><Import Project=\"$(MSBuildToolsPath)\\Microsoft.Common.targets\" /></Project>"));

        Assert.That(findings, Is.Empty);
    }

    [TestCase("build")]
    [TestCase("buildTransitive")]
    [TestCase("buildMultiTargeting")]
    public void Covers_all_three_build_roots(string root)
    {
        var findings = Inspect(new SyntheticPackage().With($"{root}/Wrong.targets", Empty));

        Assert.That(findings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Ignores_msbuild_files_outside_a_build_folder()
    {
        var findings = Inspect(new SyntheticPackage()
            .With("tools/net8.0/any/Something.targets", Empty)
            .With("contentFiles/any/any/Other.props", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Matches_the_package_id_without_regard_to_case()
    {
        var findings = Inspect(new SyntheticPackage().With("build/contoso.widgets.targets", Empty));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void Carries_a_reviewable_name()
    {
        // A code alone is not reviewable; every rule states what it is.
        Assert.That(new UnimportableBuildFolderRule().Name, Is.EqualTo("unimportable build file"));
    }
}
