using System.Text.RegularExpressions;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Checks that a package declaring itself a .NET CLI tool is actually installable.
/// </summary>
/// <remarks>
/// <para>
/// A tool package needs a <c>DotnetToolSettings.xml</c> beside its entry point, naming the
/// command and the assembly to run. Without it <c>dotnet tool install</c> fails outright with
/// "Settings file 'DotnetToolSettings.xml' was not found in the package" — for every user, on a
/// version that cannot be deleted once published.
/// </para>
/// <para>
/// Nothing earlier catches this. The project builds, packs, restores and publishes; the defect
/// only appears when somebody tries to install it, which is after the only moment it was cheap
/// to fix.
/// </para>
/// </remarks>
public sealed partial class ToolPackageRule : IPackageRule
{
    [GeneratedRegex(@"EntryPoint\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex EntryPointPattern();

    [GeneratedRegex(@"<Command\b[^>]*\bName\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CommandNamePattern();

    /// <inheritdoc />
    public string Code => "RDK0005";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        if (!package.IsDotnetTool())
        {
            yield break;
        }

        // tools/<tfm>/<rid>/ is the shape the SDK looks in; a tool with no such folder has
        // nothing to install whatever else it contains.
        var toolDirectories = package.Entries
            .Where(e => e.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) && e.Count(c => c == '/') >= 3)
            .Select(e => string.Join('/', e.Split('/').Take(3)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();

        if (toolDirectories.Count == 0)
        {
            yield return new Finding(
                Code,
                FindingSeverity.Error,
                "Declared as a .NET tool but ships no tools/<framework>/<runtime>/ folder",
                "The nuspec declares packageType DotnetTool, so the SDK will look for the tool " +
                "under tools/<framework>/<runtime>/. Nothing matches that shape, so " +
                "'dotnet tool install' has nothing to install.",
                package.Moniker);
            yield break;
        }

        foreach (var directory in toolDirectories)
        {
            var settingsPath = directory + "/DotnetToolSettings.xml";
            if (!package.Contains(settingsPath))
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Error,
                    $"{directory} has no DotnetToolSettings.xml",
                    "A tool package needs this file beside its entry point; it names the command " +
                    "and the assembly to run. Without it 'dotnet tool install' fails with " +
                    "\"Settings file 'DotnetToolSettings.xml' was not found in the package\". " +
                    "Restore and publish both succeed regardless, so this is only discovered by " +
                    "whoever tries to install it.",
                    package.Moniker);
                continue;
            }

            var settings = package.ReadText(settingsPath);
            if (settings is null)
            {
                continue;
            }

            var entryPoint = EntryPointPattern().Match(settings);
            if (!entryPoint.Success)
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Error,
                    $"{settingsPath} declares no EntryPoint",
                    "The settings file must name the assembly to run, as " +
                    "<Command Name=\"...\" EntryPoint=\"....dll\" Runner=\"dotnet\" />.",
                    package.Moniker);
                continue;
            }

            var assembly = entryPoint.Groups[1].Value;
            if (!package.Contains($"{directory}/{assembly}"))
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Error,
                    $"{settingsPath} points at {assembly}, which is not in {directory}",
                    "The entry point named in the settings file has to be shipped alongside it. " +
                    "Installation succeeds and the command fails to run.",
                    package.Moniker);
                continue;
            }

            var commandName = CommandNamePattern().Match(settings);
            if (commandName.Success && commandName.Groups[1].Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Code,
                    FindingSeverity.Warning,
                    $"{settingsPath} declares the command as '{commandName.Groups[1].Value}'",
                    "The command name is what users type, so a .dll suffix here almost certainly " +
                    "means the entry point was copied into the wrong attribute.",
                    package.Moniker);
            }
        }
    }
}
