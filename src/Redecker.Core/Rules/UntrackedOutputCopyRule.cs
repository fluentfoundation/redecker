using System.Xml.Linq;
using Redecker.Findings;
using Redecker.Packages;

namespace Redecker.Rules;

/// <summary>
/// Reports package build logic that copies files into the output directory without telling
/// MSBuild it did.
/// </summary>
/// <remarks>
/// <para>
/// MSBuild tracks what a build produced through the <c>FileWrites</c> item. <c>IncrementalClean</c>
/// uses that list to delete outputs from a previous build that the current one no longer produces,
/// and <c>Clean</c> uses it to know what to remove. A <c>Copy</c> task that does not feed its
/// <c>CopiedFiles</c> back into <c>FileWrites</c> puts files somewhere MSBuild has no record of.
/// </para>
/// <para>
/// The consequences are the awkward kind: files that survive a <c>Clean</c> because nothing knows
/// they exist, files removed by <c>IncrementalClean</c> and copied again on every build, and
/// up-to-date checks that disagree with what is actually on disk. It is worst on .NET Framework
/// targets, which predate the runtime asset resolution that makes this unnecessary on .NET Core,
/// so packages shipping native binaries to net4x hand-roll the copy and often skip the accounting.
/// </para>
/// <para>
/// A package that also ships its own clean target is a strong tell: hand-rolling <c>Clean</c> is
/// only necessary because the framework was never told about the files in the first place.
/// </para>
/// </remarks>
public sealed class UntrackedOutputCopyRule : IPackageRule
{
    // Deliberately not $(PublishDir). IncrementalClean governs the build output directory;
    // publish is a separate operation with its own lifecycle, and copying there without
    // recording FileWrites is normal rather than a hazard. Including it made coverlet.collector
    // look like a defect when it is not.
    private static readonly string[] OutputProperties =
        ["$(OutDir)", "$(OutputPath)", "$(TargetDir)"];

    /// <inheritdoc />
    public string Code => "RDK0007";

    /// <inheritdoc />
    public string Name => "untracked output copy";

    /// <inheritdoc />
    public IEnumerable<Finding> Inspect(PackageArchive package)
    {
        foreach (var file in package.MsBuildFiles())
        {
            var text = package.ReadText(file);
            if (text is null)
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(text);
            }
            catch (System.Xml.XmlException)
            {
                // RDK0001 already reports unparseable MSBuild files; no need to say it twice.
                continue;
            }

            var untracked = document.Descendants()
                .Where(e => e.Name.LocalName.Equals("Copy", StringComparison.Ordinal))
                .Where(TargetsOutputDirectory)
                .Where(c => !RecordsFileWrites(c))
                .ToList();

            if (untracked.Count == 0)
            {
                continue;
            }

            var hasCleanTarget = document.Descendants()
                .Any(e => e.Name.LocalName.Equals("Delete", StringComparison.Ordinal));

            var tell = hasCleanTarget
                ? " The file also hand-rolls a Delete of the same files, which is the giveaway: " +
                  "that is only necessary because Clean cannot know about them."
                : string.Empty;

            yield return new Finding(
                Code,
                FindingSeverity.Warning,
                $"{file} copies {untracked.Count} time(s) into the output directory without recording FileWrites",
                "MSBuild tracks build output through the FileWrites item; IncrementalClean and " +
                "Clean both work from it. A Copy that does not feed CopiedFiles back into " +
                "FileWrites leaves files MSBuild has no record of, so they can survive a Clean, " +
                "be deleted and recopied on every incremental build, or disagree with up-to-date " +
                "checks." + tell + " The fix is one element: " +
                "<Output TaskParameter=\"CopiedFiles\" ItemName=\"FileWrites\" /> inside the Copy.",
                package.Moniker);
        }
    }

    /// <summary>Whether the copy lands somewhere MSBuild considers build output.</summary>
    private static bool TargetsOutputDirectory(XElement copy) =>
        copy.Attributes()
            .Where(a => a.Name.LocalName is "DestinationFiles" or "DestinationFolder")
            .Any(a => OutputProperties.Any(p => a.Value.Contains(p, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Whether the copy, or the target containing it, tells MSBuild what was written.
    /// </summary>
    private static bool RecordsFileWrites(XElement copy)
    {
        var onTheCopy = copy.Elements()
            .Where(e => e.Name.LocalName.Equals("Output", StringComparison.Ordinal))
            .Any(e => e.Attributes()
                .Any(a => a.Name.LocalName.Equals("ItemName", StringComparison.Ordinal) &&
                          a.Value.Equals("FileWrites", StringComparison.OrdinalIgnoreCase)));

        if (onTheCopy)
        {
            return true;
        }

        // Some packages add the paths to FileWrites in a sibling ItemGroup instead, which is
        // less direct but achieves the same accounting.
        var target = copy.Ancestors()
            .FirstOrDefault(a => a.Name.LocalName.Equals("Target", StringComparison.Ordinal));

        return target is not null &&
               target.Descendants().Any(e => e.Name.LocalName.Equals("FileWrites", StringComparison.Ordinal));
    }
}
