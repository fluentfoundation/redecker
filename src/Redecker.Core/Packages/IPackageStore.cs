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
}
