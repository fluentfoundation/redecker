using System.CommandLine;
using Redecker.Findings;
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

        var command = new Command(
            "check",
            "Check the versions a repository declares for coherence. Runs offline.")
        {
            pathArgument,
        };

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetRequiredValue(pathArgument);
            var files = ProjectFiles.Resolve(path).ToList();
            if (files.Count == 0)
            {
                Console.Error.WriteLine($"error: no MSBuild files found at '{path}'.");
                return 2;
            }

            return Run(files);
        });

        return command;
    }

    internal static int Run(IReadOnlyList<string> files)
    {
        // Central package management declares versions in one file but projects may pin their
        // own, so the family has to be assembled across every file before it can be judged.
        var pins = new List<PackagePin>();
        foreach (var file in files)
        {
            pins.AddRange(PinReader.ReadFile(file));
        }

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

        return Report.Write(findings, "declared versions");
    }
}
