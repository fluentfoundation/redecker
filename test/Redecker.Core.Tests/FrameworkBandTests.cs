using NUnit.Framework;
using Redecker.Frameworks;

namespace Redecker.Tests;

[TestFixture]
public class FrameworkBandTests
{
    private static readonly string[] Available =
        ["8.0.0", "8.0.11", "9.0.0", "9.0.5", "10.0.0", "10.0.1", "11.0.0-preview.1"];

    [TestCase("net8.0", "8.0.11")]
    [TestCase("net9.0", "9.0.5")]
    [TestCase("net10.0", "10.0.1")]
    public void Keeps_a_banded_package_inside_the_band_of_the_target_framework(string tfm, string expected)
    {
        var chosen = FrameworkBand.HighestInBand("Microsoft.EntityFrameworkCore.SqlServer", tfm, Available);

        Assert.That(chosen?.ToNormalizedString(), Is.EqualTo(expected));
    }

    [Test]
    public void Takes_the_newest_version_for_a_package_that_is_not_banded()
    {
        var chosen = FrameworkBand.HighestInBand("Newtonsoft.Json", "net8.0", Available);

        Assert.That(chosen?.ToNormalizedString(), Is.EqualTo("10.0.1"));
    }

    [Test]
    public void Treats_netstandard_as_having_no_band()
    {
        // netstandard2.0 has no in-box runtime, so the band rule cannot apply.
        var chosen = FrameworkBand.HighestInBand("Microsoft.EntityFrameworkCore", "netstandard2.0", Available);

        Assert.That(chosen?.ToNormalizedString(), Is.EqualTo("10.0.1"));
    }

    [Test]
    public void Reports_nothing_rather_than_jumping_bands_when_the_band_is_empty()
    {
        // Silently taking 10.0.1 for a net12.0 project would be exactly the bug this exists to
        // prevent, so an empty band is a signal, not a reason to fall back.
        var chosen = FrameworkBand.HighestInBand("Microsoft.EntityFrameworkCore", "net12.0", Available);

        Assert.That(chosen, Is.Null);
    }

    [Test]
    public void Excludes_prerelease_unless_asked()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                FrameworkBand.HighestInBand("Newtonsoft.Json", "net8.0", Available)?.ToNormalizedString(),
                Is.EqualTo("10.0.1"));
            Assert.That(
                FrameworkBand.HighestInBand("Newtonsoft.Json", "net8.0", Available, allowPrerelease: true)
                    ?.ToNormalizedString(),
                Is.EqualTo("11.0.0-preview.1"));
        });
    }

    // The whole EF Core family is bound to the runtime generation it ships alongside.
    [TestCase("Microsoft.EntityFrameworkCore", true)]
    [TestCase("Microsoft.EntityFrameworkCore.SqlServer", true)]
    [TestCase("Microsoft.EntityFrameworkCore.Relational", true)]
    [TestCase("Microsoft.EntityFrameworkCore.InMemory", true)]
    [TestCase("Microsoft.EntityFrameworkCore.Tools", true)]

    // Shipped outside the shared framework, but written against a specific ASP.NET Core.
    [TestCase("Microsoft.AspNetCore.OpenApi", true)]
    [TestCase("Microsoft.AspNetCore.Identity.EntityFrameworkCore", true)]
    [TestCase("Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore", true)]

    // Taking these across a generation lifts them out of the shared framework and ships them
    // app-local and unoptimised.
    [TestCase("Microsoft.Extensions.Hosting", true)]
    [TestCase("Microsoft.Extensions.DependencyInjection", true)]
    [TestCase("Microsoft.Extensions.Configuration", true)]
    [TestCase("Microsoft.Extensions.Http.Polly", true)]
    [TestCase("System.Diagnostics.DiagnosticSource", true)]
    [TestCase("System.Text.Json", true)]

    // The correction that matters: most of Microsoft.Extensions.* is compile-at-head. A blanket
    // prefix test would hold these at 8.x for no reason at all.
    [TestCase("Microsoft.Extensions.Caching.Memory", false)]
    [TestCase("Microsoft.Extensions.Options", false)]
    [TestCase("Microsoft.Extensions.Logging.Abstractions", false)]
    [TestCase("Microsoft.Extensions.Primitives", false)]

    // Nor is every System.* package runtime-bound.
    [TestCase("System.CommandLine", false)]
    [TestCase("System.Linq.Async", false)]

    [TestCase("Newtonsoft.Json", false)]
    [TestCase("Microsoft.Data.SqlClient", false)]
    public void Recognises_which_packages_are_actually_band_bound(string packageId, bool expected)
    {
        Assert.That(FrameworkBand.IsBanded(packageId), Is.EqualTo(expected));
    }

    [Test]
    public void Lets_a_repository_state_its_own_policy()
    {
        // The default is a starting point, not a law: a project that knows its own constraints
        // should be able to say so.
        var policy = new BandPolicy(bandedIds: ["Contoso.Runtime.Bound"]);

        Assert.Multiple(() =>
        {
            Assert.That(FrameworkBand.IsBanded("Contoso.Runtime.Bound", policy), Is.True);
            Assert.That(FrameworkBand.IsBanded("Microsoft.EntityFrameworkCore", policy), Is.False);
        });
    }
}
