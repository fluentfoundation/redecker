using System.CommandLine;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Projects;
using Redecker.Rules;

namespace Redecker.Cli.Commands;

/// <summary>
/// <c>redecker check</c>: runs the rules that read a repository's declared versions rather than a
/// package's contents.
/// </summary>
/// <remarks>
/// Separate from <c>inspect</c> because the subject is different. <c>inspect</c> answers "is this
/// package version sound?" and needs the network; this answers "are the versions this repository
/// declares coherent with each other?" and needs nothing but the files on disk.
/// </remarks>
public static class CheckCommand
{
    /// <summary>Builds the command.</summary>
    public static Command Create()
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "A project file, Directory.Packages.props, or a directory containing one.",
            DefaultValueFactory = _ => ".",
        };

        // Opt-in rather than automatic. Everything else this command does reads files on disk, and
        // silently reaching the network because a rule was added would be a surprise in CI.
        var onlineOption = new Option<bool>("--online")
        {
            Description =
                "Also check constraints that declared packages place on each other, which requires " +
                "reading their nuspecs from nuget.org.",
        };

        var command = new Command(
            "check",
            "Check the versions a repository declares for coherence. Runs offline unless --online.")
        {
            pathArgument,
            onlineOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetRequiredValue(pathArgument);
            var files = ProjectFiles.Resolve(path).ToList();
            if (files.Count == 0)
            {
                Console.Error.WriteLine($"error: no MSBuild files found at '{path}'.");
                return 2;
            }

            if (!parseResult.GetValue(onlineOption))
            {
                return Run(files);
            }

            // Shares inspect's download cache: the same nuspecs are usually wanted by both, and a
            // package version never changes, so a hit is always valid.
            using var store = new FlatContainerPackageStore(
                cacheDirectory: InspectCommand.CacheDirectory());
            return await RunAsync(files, store, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>The offline rules, plus the one that needs nuget.org.</summary>
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> files,
        IPackageStore store,
        CancellationToken cancellationToken)
    {
        var pins = ReadPins(files);
        var findings = Offline(pins, files);

        Console.WriteLine("Reading declared constraints from nuget.org...");
        findings.AddRange(
            await new TrackingConstraintRule().InspectAsync(pins, store, cancellationToken)
                .ConfigureAwait(false));

        return Report.Write(findings, "declared versions");
    }

    internal static int Run(IReadOnlyList<string> files)
    {
        var pins = ReadPins(files);
        return Report.Write(Offline(pins, files), "declared versions");
    }

    private static List<PackagePin> ReadPins(IReadOnlyList<string> files)
    {
        // Central package management declares versions in one file but projects may pin their
        // own, so the family has to be assembled across every file before it can be judged.
        var pins = new List<PackagePin>();
        foreach (var file in files)
        {
            pins.AddRange(PinReader.ReadFile(file));
        }

        return pins;
    }

    private static List<Finding> Offline(List<PackagePin> pins, IReadOnlyList<string> files)
    {
        var findings = new List<Finding>(new LockstepFamilyRule().Inspect(pins));
        findings.AddRange(new UndocumentedTransitivePinRule().Inspect(pins));

        var references = pins.Count(p => p.ItemType.Equals("PackageReference", StringComparison.Ordinal));
        Console.WriteLine(
            $"{pins.Count} declaration(s) across {files.Count} file(s); {references} direct reference(s).");

        if (references == 0)
        {
            // Worth saying out loud: silence here would look like a clean bill of health when in
            // fact one rule could not run at all.
            Console.WriteLine(
                "  note: no PackageReference was found, so RDK0004 was skipped. Point check at a " +
                "directory containing the projects, not only at Directory.Packages.props.");
        }

        return findings;
    }
}
