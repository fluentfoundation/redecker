using NuGet.Frameworks;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports an assembly under <c>lib/&lt;framework&gt;/</c> that a project targeting that framework
/// could not actually load.
/// </summary>
/// <remarks>
/// <para>
/// The folder name is a promise. Restore reads it, picks the best match for the consuming project,
/// and hands over whatever is inside without ever checking that the assembly agrees. A
/// <c>net472</c> assembly in <c>lib/net45/</c> restores cleanly and then fails the consumer's
/// build; a .NET Framework assembly in <c>lib/netstandard2.0/</c> restores cleanly and then fails
/// at run time on .NET.
/// </para>
/// <para>
/// <b>Differing is not the defect.</b> A <c>netstandard2.0</c> assembly in <c>lib/net8.0/</c> is
/// the standard way to win nearest-framework matching, and a <c>net45</c> assembly in
/// <c>lib/net452/</c> is a build that was reused rather than repeated. Across 4,205 packages, 75
/// such pairings differ and work. The question is not whether the two strings match but whether a
/// project targeting the folder can consume the assembly — which is the question restore itself
/// asks, so this rule asks NuGet rather than re-deriving version arithmetic that already exists.
/// </para>
/// <para>
/// <b>Scoped to frameworks anyone still ships to.</b> Half of every incompatible pairing in that
/// survey sat in a dead platform — PCL profiles, Silverlight, Windows Phone, Windows Store,
/// MonoAndroid, Xamarin, UAP, Tizen — where <c>MonoAndroid403</c> is an OS version, <c>uap10.0</c>
/// maps to <c>.NETCore,Version=v5.0</c>, and a profile is a set intersection rather than a version.
/// Those comparisons are not wrong, but the tooling that would republish those packages no longer
/// exists, and a rule nobody can act on is a rule people suppress.
/// </para>
/// <para>
/// A warning rather than an error. <c>Microsoft.CodeCoverage</c> ships a .NET Framework 4.0 shim
/// under <c>lib/net8.0/</c> on purpose, and it is installed in a large share of the test projects
/// in existence — gating a build on this would fail far more work than it saves.
/// </para>
/// </remarks>
public sealed class FrameworkFolderMismatchRule : IPackageRule
{
    /// <inheritdoc />
    public string Code => "RDK0010";

    /// <inheritdoc />
    public string Name => "assembly does not match its framework folder";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        foreach (var group in Mismatches(package)
                     .GroupBy(m => (m.Folder, m.Declared))
                     .OrderBy(g => g.Key.Folder, StringComparer.Ordinal)
                     .ThenBy(g => g.Key.Declared, StringComparer.Ordinal))
        {
            var files = group.Select(m => m.Entry).OrderBy(e => e, StringComparer.Ordinal).ToList();
            var own = group.Any(m => m.Own);

            yield return new Finding(
                Code,
                FindingSeverity.Warning,
                $"lib/{group.Key.Folder}/ contains an assembly built for {group.Key.Declared}",
                $"{string.Join(", ", files.Take(3))}" +
                (files.Count > 3 ? $" and {files.Count - 3} more" : "") +
                (files.Count == 1 ? " declares " : " declare ") + group.Key.Declared +
                $", which a project targeting {group.Key.Folder} cannot consume. Restore does not " +
                "check this — it reads the folder name, hands over what is inside, and the failure " +
                "surfaces later as an unresolved reference or a missing type at run time. " +
                (files.Count == 1
                    ? own
                        ? "This is the package's own assembly, so the fix is in its build."
                        : "This is a bundled dependency, so the folder is promising something " +
                          "about somebody else's build."
                    : own
                        ? "These are the package's own assemblies, so the fix is in its build."
                        : "These are bundled dependencies, so the folder is promising something " +
                          "about somebody else's build.") +
                (files.Count == 1
                    ? $" Either pack it under a folder it satisfies, or build it for {group.Key.Folder}."
                    : $" Either pack them under a folder they satisfy, or build them for {group.Key.Folder}."),
                package.Moniker);
        }
    }

    private static IEnumerable<(string Entry, string Folder, string Declared, bool Own)> Mismatches(
        PackageArchive package)
    {
        foreach (var entry in package.Entries.OrderBy(e => e, StringComparer.Ordinal))
        {
            if (!IsShippedAssembly(entry))
            {
                continue;
            }

            var folder = entry.Split('/')[1];
            if (package.ReadBytes(entry) is not { } image ||
                !ManagedAssembly.TryReadTargetFramework(image, out var declared) ||
                declared is null ||
                !IsLiving(folder, declared))
            {
                continue;
            }

            var folderFramework = NuGetFramework.ParseFolder(folder);
            NuGetFramework declaredFramework;
            try
            {
                declaredFramework = NuGetFramework.Parse(declared);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (folderFramework.IsUnsupported || declaredFramework.IsUnsupported ||
                DefaultCompatibilityProvider.Instance.IsCompatible(folderFramework, declaredFramework))
            {
                continue;
            }

            var own = string.Equals(
                Path.GetFileNameWithoutExtension(entry), package.Id, StringComparison.OrdinalIgnoreCase);

            yield return (entry, folder, declared, own);
        }
    }

    /// <summary>
    /// Assemblies a consumer would compile against: directly in <c>lib/&lt;framework&gt;/</c>, and
    /// not a satellite.
    /// </summary>
    private static bool IsShippedAssembly(string entry) =>
        entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
        entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
        !entry.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) &&
        entry.Count(c => c == '/') == 2;

    /// <summary>
    /// Whether both sides are frameworks anyone still ships to: .NET Framework, .NET Core / .NET 5+,
    /// and .NET Standard.
    /// </summary>
    private static bool IsLiving(string folder, string declared)
    {
        if (declared.Split(',')[0] is not (".NETFramework" or ".NETCoreApp" or ".NETStandard"))
        {
            return false;
        }

        var name = folder.ToLowerInvariant();
        if (!name.StartsWith("net", StringComparison.Ordinal))
        {
            return false;
        }

        // "netcore45"/"netcore451" are Windows Store, not .NET Core; "netcoreapp" is the real thing.
        return !name.StartsWith("netcore", StringComparison.Ordinal) ||
               name.StartsWith("netcoreapp", StringComparison.Ordinal);
    }
}
