#if SPIKE_NUGET_PACKAGING
using NuGet.Packaging;
using NUnit.Framework;

namespace Redecker.Tests;

/// <summary>
/// A spike, not a test suite: does NuGet.Packaging already encode the conventions Redecker
/// hand-rolls? Compiled only when SPIKE_NUGET_PACKAGING is defined.
/// </summary>
[TestFixture]
[Category("Network")]
public class SpikeNuGetPackagingTests
{
    [Test]
    public void What_does_the_official_reader_give_us()
    {
        var path = Directory.GetFiles(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../artifacts/packages"),
            "dotnet-redecker.*.nupkg").First();

        using var reader = new PackageArchiveReader(path);

        TestContext.Out.WriteLine($"identity:     {reader.GetIdentity()}");
        TestContext.Out.WriteLine($"packageTypes: {string.Join(", ", reader.NuspecReader.GetPackageTypes())}");
        TestContext.Out.WriteLine($"repository:   {reader.NuspecReader.GetRepositoryMetadata()?.Url}");

        // The interesting one: does GetBuildItems() apply the <id>.props/.targets convention?
        foreach (var group in reader.GetBuildItems())
        {
            TestContext.Out.WriteLine($"build [{group.TargetFramework}]: {string.Join(", ", group.Items)}");
        }

        foreach (var group in reader.GetToolItems())
        {
            TestContext.Out.WriteLine($"tools [{group.TargetFramework}]: {group.Items.Count()} items");
        }

        foreach (var group in reader.GetLibItems())
        {
            TestContext.Out.WriteLine($"lib   [{group.TargetFramework}]: {group.Items.Count()} items");
        }

        Assert.Pass();
    }
}
#endif
