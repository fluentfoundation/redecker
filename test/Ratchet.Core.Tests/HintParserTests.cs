using NUnit.Framework;
using Ratchet.Hints;

namespace Ratchet.Tests;

[TestFixture]
public class HintParserTests
{
    [Test]
    public void Parses_a_security_floor_hint_with_a_transitive_exit_condition()
    {
        var parsed = HintParser.TryParse(
            "security-floor: #:package Newtonsoft.Json@13.0.0; until: transitive-floor(Some.Package) >= 13.0.0",
            out var hint,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(hint!.Kind, Is.EqualTo(HintKind.SecurityFloor));
            Assert.That(hint.PackageId, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(hint.Version, Is.EqualTo("13.0.0"));
            Assert.That(hint.Exit, Is.TypeOf<ExitCondition.TransitiveFloor>());
        });

        var floor = (ExitCondition.TransitiveFloor)hint!.Exit!;
        Assert.Multiple(() =>
        {
            Assert.That(floor.PackageId, Is.EqualTo("Some.Package"));
            Assert.That(floor.Version, Is.EqualTo("13.0.0"));
        });
    }

    [Test]
    public void Parses_the_upstream_bug_hint_that_describes_the_sqlite_hold()
    {
        var parsed = HintParser.TryParse(
            "upstream-bug: #:package SQLitePCLRaw.bundle_e_sqlite3@2.1.11; " +
            "until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12); " +
            "note: net461 targets copies runtimes/win-arm/native/e_sqlite3.dll; 2.1.12 stopped shipping it",
            out var hint,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(hint!.Kind, Is.EqualTo(HintKind.UpstreamBug));
            Assert.That(hint.Exit, Is.TypeOf<ExitCondition.PackageAssetsIntact>());
        });

        var intact = (ExitCondition.PackageAssetsIntact)hint!.Exit!;
        Assert.Multiple(() =>
        {
            Assert.That(intact.PackageId, Is.EqualTo("SQLitePCLRaw.lib.e_sqlite3"));
            Assert.That(intact.Version, Is.EqualTo("2.1.12"));
            // The note contains a semicolon, which must not be read as another segment.
            Assert.That(hint.Note, Does.Contain("2.1.12 stopped shipping it"));
        });
    }

    [TestCase("never", typeof(ExitCondition.Never))]
    [TestCase("review", typeof(ExitCondition.Review))]
    [TestCase("advisory-clear(GHSA-2m69-gcr7-jv3q)", typeof(ExitCondition.AdvisoryClear))]
    public void Parses_each_exit_condition_form(string condition, Type expected)
    {
        var parsed = HintParser.TryParse(
            $"api-compat: #:package X@1.0.0; until: {condition}", out var hint, out _);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(hint!.Exit, Is.TypeOf(expected));
        });
    }

    [Test]
    public void Accepts_a_hint_without_a_version()
    {
        var parsed = HintParser.TryParse("framework-band: #:package Microsoft.Extensions.Logging; until: never",
            out var hint, out _);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(hint!.Version, Is.Null);
            Assert.That(hint.Kind, Is.EqualTo(HintKind.FrameworkBand));
        });
    }

    [TestCase("test")]
    [TestCase("Some grouping label")]
    [TestCase("")]
    [TestCase(null)]
    public void Leaves_ordinary_labels_alone(string? label)
    {
        // Label is used for plain grouping, so a non-hint must parse as "not a hint" rather than
        // as an error, or every existing repository lights up with false problems.
        var parsed = HintParser.TryParse(label, out var hint, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(hint, Is.Null);
            Assert.That(error, Is.Null);
        });
    }

    [TestCase("security-floor: Newtonsoft.Json", "not of the form")]
    [TestCase("security-floor: #:package X@1.0; until: nonsense(Y)", "Unknown exit condition")]
    [TestCase("security-floor: #:package X@1.0; whatever: Y", "Unrecognised hint segment")]
    [TestCase("security-floor: #:package X@1.0; until: transitive-floor(Y)", "expects a comparison")]
    public void Reports_a_malformed_hint_rather_than_ignoring_it(string label, string expected)
    {
        var parsed = HintParser.TryParse(label, out var hint, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(hint, Is.Null);
            Assert.That(error, Does.Contain(expected));
        });
    }
}
