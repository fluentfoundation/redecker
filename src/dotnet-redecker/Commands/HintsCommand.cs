using System.CommandLine;
using Redecker.Hints;
using Redecker.Packages;
using Redecker.Projects;

namespace Redecker.Cli.Commands;

/// <summary>
/// <c>redecker hints</c>: lists the pins in a project or Directory.Packages.props, and re-evaluates
/// each recorded exit condition so that pins which have outlived their reason can be removed.
/// </summary>
public static class HintsCommand
{
    /// <summary>Builds the command.</summary>
    public static Command Create()
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "A project file, Directory.Packages.props, or a directory containing one.",
            DefaultValueFactory = _ => ".",
        };

        var checkOption = new Option<bool>("--check")
        {
            Description = "Re-evaluate exit conditions; exits non-zero if any pin can be retired.",
        };

        var sourceOption = new Option<string>("--source")
        {
            Description = "NuGet V3 flat container base URL.",
            DefaultValueFactory = _ => FlatContainerPackageStore.NuGetOrg,
        };

        var command = new Command("hints", "List pin rationales and check whether they still apply.")
        {
            pathArgument,
            checkOption,
            sourceOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetRequiredValue(pathArgument);
            var check = parseResult.GetValue(checkOption);
            var source = parseResult.GetRequiredValue(sourceOption);

            var files = ResolveFiles(path).ToList();
            if (files.Count == 0)
            {
                Console.Error.WriteLine($"error: no MSBuild files found at '{path}'.");
                return 2;
            }

            using var store = new FlatContainerPackageStore(source, InspectCommand.CacheDirectory());
            return await RunAsync(files, check, store, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(
        IReadOnlyList<string> files,
        bool check,
        IPackageStore store,
        CancellationToken cancellationToken)
    {
        var evaluator = new HintEvaluator(store);
        var hinted = 0;
        var retirable = 0;
        var malformed = 0;

        foreach (var file in files)
        {
            foreach (var pin in PinReader.ReadFile(file))
            {
                if (pin.HintError is not null)
                {
                    malformed++;
                    Console.WriteLine($"{Location(pin)} {pin.PackageId}");
                    Console.WriteLine($"    malformed hint: {pin.HintError}");
                    Console.WriteLine();
                    continue;
                }

                if (pin.Hint is null)
                {
                    continue;
                }

                hinted++;
                Console.WriteLine($"{Location(pin)} {pin.PackageId} {pin.Version}");
                Console.WriteLine($"    kind: {pin.Hint.Kind}");
                if (pin.Hint.Note is not null)
                {
                    Console.WriteLine($"    note: {pin.Hint.Note}");
                }

                Console.WriteLine($"    until: {pin.Hint.Exit?.ToString() ?? "(none recorded)"}");

                if (check)
                {
                    var verdict = await evaluator.EvaluateAsync(pin.Hint, cancellationToken)
                        .ConfigureAwait(false);
                    Console.WriteLine($"    status: {verdict.Status} - {verdict.Explanation}");
                    if (verdict.Status == PinStatus.Retirable)
                    {
                        retirable++;
                    }
                }

                Console.WriteLine();
            }
        }

        Console.WriteLine($"{hinted} hinted pin(s), {malformed} malformed.");
        if (check)
        {
            Console.WriteLine($"{retirable} pin(s) can now be retired.");
        }

        if (malformed > 0)
        {
            return 2;
        }

        return check && retirable > 0 ? 1 : 0;
    }

    private static string Location(PackagePin pin) =>
        pin.Line > 0 ? $"{pin.File}:{pin.Line}" : pin.File;

    private static IEnumerable<string> ResolveFiles(string path)
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
        candidates.AddRange(Directory.GetFiles(path, "Directory.Packages.props", SearchOption.AllDirectories));
        candidates.AddRange(Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories));
        return candidates
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal);
    }
}
