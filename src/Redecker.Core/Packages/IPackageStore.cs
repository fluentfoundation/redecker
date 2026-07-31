namespace Redecker.Packages;

/// <summary>Somewhere packages can be fetched from.</summary>
public interface IPackageStore
{
    /// <summary>
    /// Fetches a package, or returns <see langword="null"/> if that version does not exist.
    /// </summary>
    Task<PackageArchive?> GetAsync(string id, string version, CancellationToken cancellationToken);

    /// <summary>All published versions of <paramref name="id"/>, oldest first.</summary>
    Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches a package's symbols, or returns <see langword="null"/> when none were published.
    /// </summary>
    /// <remarks>
    /// Symbol packages are not served from the flat container — the obvious URL 404s. They live on
    /// a separate symbol endpoint, which is why this is not simply another call to
    /// <see cref="GetAsync"/>.
    /// </remarks>
    Task<PackageArchive?> GetSymbolsAsync(string id, string version, CancellationToken cancellationToken);
}
