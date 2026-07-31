using System.IO.Compression;
using System.Text;
using Redecker.Packages;

namespace Redecker.Tests;

/// <summary>
/// Builds a .nupkg in memory so rule tests are fast, offline, and state exactly the package shape
/// they depend on rather than relying on whatever a real package happens to contain today.
/// </summary>
internal sealed class SyntheticPackage
{
    private readonly Dictionary<string, byte[]> _entries = new(StringComparer.OrdinalIgnoreCase);

    public SyntheticPackage With(string path, string content = "")
    {
        _entries[path] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <summary>Adds an entry with raw bytes, for rules that read a real assembly image.</summary>
    public SyntheticPackage With(string path, byte[] content)
    {
        _entries[path] = content;
        return this;
    }

    /// <summary>Adds an MSBuild file that copies each of <paramref name="relativePaths"/>.</summary>
    public SyntheticPackage WithCopyTargets(string path, params string[] relativePaths)
    {
        var items = string.Join(
            Environment.NewLine,
            relativePaths.Select(p =>
                $"""
                     <None Include="$(MSBuildThisFileDirectory){p}">
                       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                     </None>
                 """));

        return With(path,
            $"""
             <Project>
               <ItemGroup>
             {items}
               </ItemGroup>
             </Project>
             """);
    }

    public PackageArchive Build(string id = "Test.Package", string version = "1.0.0")
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in _entries)
            {
                var entry = zip.CreateEntry(path);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }
        }

        buffer.Position = 0;
        return PackageArchive.Open(id, version, buffer);
    }
}
