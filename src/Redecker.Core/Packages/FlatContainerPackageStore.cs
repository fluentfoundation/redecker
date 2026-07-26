using System.Net;
using System.Text.Json;

namespace Redecker.Packages;

/// <summary>
/// Reads packages from a NuGet V3 flat container. Downloads are cached on disk, because the
/// rules re-open the same package for several checks and an upgrade sweep would otherwise pull
/// the same bytes repeatedly.
/// </summary>
public sealed class FlatContainerPackageStore : IPackageStore, IDisposable
{
    /// <summary>The flat container for nuget.org.</summary>
    public const string NuGetOrg = "https://api.nuget.org/v3-flatcontainer";

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;
    private readonly string? _cacheDirectory;

    /// <param name="baseUrl">Flat container base URL; defaults to <see cref="NuGetOrg"/>.</param>
    /// <param name="cacheDirectory">Where to cache downloads, or <see langword="null"/> to disable.</param>
    /// <param name="httpClient">An HTTP client to borrow; one is created when this is null.</param>
    public FlatContainerPackageStore(
        string? baseUrl = null,
        string? cacheDirectory = null,
        HttpClient? httpClient = null)
    {
        _baseUrl = (baseUrl ?? NuGetOrg).TrimEnd('/');
        _cacheDirectory = cacheDirectory;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();

        if (_cacheDirectory is not null)
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <inheritdoc />
    public async Task<PackageArchive?> GetAsync(string id, string version, CancellationToken cancellationToken)
    {
        var lowerId = id.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        var fileName = $"{lowerId}.{lowerVersion}.nupkg";

        if (_cacheDirectory is not null)
        {
            var cached = Path.Combine(_cacheDirectory, fileName);
            if (File.Exists(cached))
            {
                return PackageArchive.Open(id, version, File.OpenRead(cached));
            }
        }

        var url = $"{_baseUrl}/{lowerId}/{lowerVersion}/{fileName}";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (_cacheDirectory is not null)
        {
            // Write via a temp file so a cancelled download cannot leave a truncated package
            // behind that every later run would happily read.
            var target = Path.Combine(_cacheDirectory, fileName);
            var temp = target + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, target, overwrite: true);
        }

        return PackageArchive.Open(id, version, new MemoryStream(bytes, writable: false));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/{id.ToLowerInvariant()}/index.json";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("versions", out var versions))
        {
            return [];
        }

        return versions.EnumerateArray().Select(v => v.GetString()!).Where(v => v is not null).ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
