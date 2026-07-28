using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using Redecker.Packages;

namespace Redecker.Tests;

[TestFixture]
public class PackageArchiveFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "redecker-file-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteNupkg(string fileName, params (string Path, string Content)[] entries)
    {
        var path = Path.Combine(_directory, fileName);
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            using var writer = zip.CreateEntry(entryPath).Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            writer.Write(bytes, 0, bytes.Length);
        }

        return path;
    }

    [Test]
    public void Takes_identity_from_the_nuspec_rather_than_the_file_name()
    {
        // The file name is a convention, not a guarantee; the nuspec is authoritative.
        var path = WriteNupkg(
            "whatever-someone-renamed-it-to.nupkg",
            ("Contoso.Widgets.nuspec",
                "<package><metadata><id>Contoso.Widgets</id><version>2.5.0</version></metadata></package>"));

        using var package = PackageArchive.OpenFile(path);

        Assert.That(package.Moniker, Is.EqualTo("Contoso.Widgets@2.5.0"));
    }

    [Test]
    public void Falls_back_to_the_file_name_when_there_is_no_nuspec()
    {
        var path = WriteNupkg("Fallback.Package.nupkg", ("lib/net8.0/x.dll", ""));

        using var package = PackageArchive.OpenFile(path);

        Assert.That(package.Id, Is.EqualTo("Fallback.Package"));
    }

    [Test]
    public void Reads_entries_so_the_rules_can_run_against_a_file_on_disk()
    {
        var path = WriteNupkg(
            "Broken.Package.nupkg",
            ("Broken.Package.nuspec",
                "<package><metadata><id>Broken.Package</id><version>1.0.0</version></metadata></package>"),
            ("buildTransitive/net461/Broken.Package.targets",
                """
                <Project>
                  <ItemGroup>
                    <None Include="$(MSBuildThisFileDirectory)../../runtimes/win-arm/native/x.dll" />
                  </ItemGroup>
                </Project>
                """));

        using var package = PackageArchive.OpenFile(path);
        var findings = new Redecker.Rules.DanglingAssetRule().Inspect(package).ToList();

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Title, Does.Contain("runtimes/win-arm/native/x.dll"));
    }
}
