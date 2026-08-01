using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Redecker.Packages;

/// <summary>
/// A read-only view over the entries of a <c>.nupkg</c>. Rules work against this rather than a
/// stream so that tests can build a package in memory instead of reaching the network.
/// </summary>
public sealed class PackageArchive : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly HashSet<string> _entries;

    private PackageArchive(string id, string version, ZipArchive archive)
    {
        Id = id;
        Version = version;
        _archive = archive;
        _entries = new HashSet<string>(
            archive.Entries.Select(e => Normalize(e.FullName)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The package identifier, as supplied by the caller.</summary>
    public string Id { get; }

    /// <summary>The package version, as supplied by the caller.</summary>
    public string Version { get; }

    /// <summary><c>id@version</c>, for use as a <see cref="Findings.Finding.Subject"/>.</summary>
    public string Moniker => $"{Id}@{Version}";

    /// <summary>Every entry path in the package, normalised to forward slashes.</summary>
    public IReadOnlyCollection<string> Entries => _entries;

    /// <summary>Opens an archive over <paramref name="stream"/>, which it takes ownership of.</summary>
    public static PackageArchive Open(string id, string version, Stream stream) =>
        new(id, version, new ZipArchive(stream, ZipArchiveMode.Read));

    /// <summary>
    /// Opens a <c>.nupkg</c> from disk, taking its identity from the nuspec inside.
    /// </summary>
    /// <remarks>
    /// Lets a package be checked before it is published, which is the only point at which a
    /// finding is still cheap to act on. Redecker's own CI uses this on its packed output.
    /// </remarks>
    public static PackageArchive OpenFile(string path)
    {
        var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);

        var nuspec = archive.Entries.FirstOrDefault(
            e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
                 !e.FullName.Contains('/'));

        var id = Path.GetFileNameWithoutExtension(path);
        var version = "unknown";

        if (nuspec is not null)
        {
            using var reader = new StreamReader(nuspec.Open());
            var text = reader.ReadToEnd();
            var idMatch = Regex.Match(text, @"<id>\s*([^<]+?)\s*</id>", RegexOptions.IgnoreCase);
            var versionMatch = Regex.Match(text, @"<version>\s*([^<]+?)\s*</version>", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                id = idMatch.Groups[1].Value;
            }

            if (versionMatch.Success)
            {
                version = versionMatch.Groups[1].Value;
            }
        }

        return new PackageArchive(id, version, archive);
    }

    /// <summary>Whether the package contains <paramref name="path"/> (case-insensitive).</summary>
    public bool Contains(string path) => _entries.Contains(Normalize(path));

    /// <summary>Reads an entry as text, or returns <see langword="null"/> if it is absent.</summary>
    public string? ReadText(string path)
    {
        var entry = _archive.Entries.FirstOrDefault(
            e => string.Equals(Normalize(e.FullName), Normalize(path), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>Reads an entry as bytes, or returns <see langword="null"/> if it is absent.</summary>
    /// <remarks>
    /// Returns an array rather than the entry's stream because a zip entry cannot seek, and
    /// anything that reads a PE file needs random access.
    /// </remarks>
    public byte[]? ReadBytes(string path)
    {
        var entry = _archive.Entries.FirstOrDefault(
            e => string.Equals(Normalize(e.FullName), Normalize(path), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        using (var stream = entry.Open())
        {
            stream.CopyTo(buffer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The MSBuild files a consuming project would import: everything under <c>build</c>,
    /// <c>buildTransitive</c>, and <c>buildMultiTargeting</c>. These are the files whose dangling
    /// references break a restore-clean upgrade at build time.
    /// </summary>
    public IEnumerable<string> MsBuildFiles() =>
        _entries.Where(e =>
            (e.StartsWith("build/", StringComparison.OrdinalIgnoreCase) ||
             e.StartsWith("buildTransitive/", StringComparison.OrdinalIgnoreCase) ||
             e.StartsWith("buildMultiTargeting/", StringComparison.OrdinalIgnoreCase)) &&
            (e.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
             e.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)))
        .OrderBy(e => e, StringComparer.Ordinal);

    /// <summary>The package's nuspec as text, or <see langword="null"/> if it has none.</summary>
    public string? Nuspec()
    {
        var path = _entries.FirstOrDefault(
            e => e.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.Contains('/'));
        return path is null ? null : ReadText(path);
    }

    /// <summary>The version ranges this package declares on other packages.</summary>
    public IReadOnlyList<PackageDependency> Dependencies() => NuspecDependencies.Read(Nuspec());

    /// <summary>
    /// Whether the package declares itself a .NET CLI tool, which brings structural requirements
    /// nothing else has.
    /// </summary>
    public bool IsDotnetTool() =>
        Nuspec() is { } nuspec &&
        Regex.IsMatch(nuspec, @"<packageType[^>]*\bname\s*=\s*""DotnetTool""", RegexOptions.IgnoreCase);

    /// <summary>
    /// The source repository the package declares in its nuspec, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Packages already carry this, so a hint about upstream issues need not spell out a URL:
    /// naming the package is enough to find where its issues live. Falls back to
    /// <c>projectUrl</c>, which older packages use in place of <c>repository</c>, but only when
    /// that points at a source host rather than a documentation site.
    /// </remarks>
    public string? RepositoryUrl()
    {
        var nuspecPath = _entries.FirstOrDefault(
            e => e.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.Contains('/'));
        var nuspec = nuspecPath is null ? null : ReadText(nuspecPath);
        if (nuspec is null)
        {
            return null;
        }

        var repository = Regex.Match(
            nuspec, @"<repository[^>]*\burl\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (repository.Success)
        {
            return repository.Groups[1].Value;
        }

        var project = Regex.Match(
            nuspec, @"<projectUrl>\s*([^<]+?)\s*</projectUrl>", RegexOptions.IgnoreCase);
        return project.Success &&
               project.Groups[1].Value.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            ? project.Groups[1].Value
            : null;
    }

    /// <summary>The target framework folder names under <c>lib/</c>.</summary>
    public IReadOnlySet<string> LibFrameworks() => SecondSegmentsUnder("lib/");

    /// <summary>The runtime identifier folder names under <c>runtimes/</c>.</summary>
    public IReadOnlySet<string> RuntimeIdentifiers() => SecondSegmentsUnder("runtimes/");

    private HashSet<string> SecondSegmentsUnder(string prefix)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _entries)
        {
            if (!entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = entry[prefix.Length..];
            var slash = rest.IndexOf('/');
            // Only count folders: a bare file directly under lib/ has no framework to report.
            if (slash > 0)
            {
                result.Add(rest[..slash]);
            }
        }

        return result;
    }

    internal static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <inheritdoc />
    public void Dispose() => _archive.Dispose();
}
