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
    public void Keeps_an_in_box_package_inside_the_band_of_the_target_framework(string tfm, string expected)
    {
        var chosen = FrameworkBand.HighestInBand("Microsoft.Extensions.Logging", tfm, Available);

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
        var chosen = FrameworkBand.HighestInBand("System.Text.Json", "netstandard2.0", Available);

        Assert.That(chosen?.ToNormalizedString(), Is.EqualTo("10.0.1"));
    }

    [Test]
    public void Reports_nothing_rather_than_jumping_bands_when_the_band_is_empty()
    {
        // Silently taking 10.0.1 for a net12.0 project would be exactly the bug this exists to
        // prevent, so an empty band is a signal, not a reason to fall back.
        var chosen = FrameworkBand.HighestInBand("Microsoft.Extensions.Logging", "net12.0", Available);

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

    [TestCase("System.Text.Json", true)]
    [TestCase("Microsoft.Extensions.Logging", true)]
    [TestCase("Microsoft.AspNetCore.JsonPatch", true)]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", true)]
    [TestCase("Newtonsoft.Json", false)]
    [TestCase("Microsoft.Data.SqlClient", false)]
    public void Recognises_the_in_box_families(string packageId, bool expected)
    {
        Assert.That(FrameworkBand.IsBanded(packageId), Is.EqualTo(expected));
    }
}
