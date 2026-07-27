namespace Redecker.Cli.Commands;

/// <summary>Locating the MSBuild files a command should read.</summary>
internal static class ProjectFiles
{
    /// <summary>
    /// Expands a path into the MSBuild files under it, or returns the file itself.
    /// </summary>
    public static IEnumerable<string> Resolve(string path)
    {
        if (File.Exists(path))
        {
            return [path];
        }

        if (!Directory.Exists(path))
        {
            return [];
        }

        var candidates = new List<string>();
        candidates.AddRange(
            Directory.GetFiles(path, "Directory.Packages.props", SearchOption.AllDirectories));
        candidates.AddRange(Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories));

        // Build output contains generated project files that mirror the real ones; reading them
        // would double-count every declared version.
        return candidates
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal);
    }
}
