using System.IO.Compression;

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
