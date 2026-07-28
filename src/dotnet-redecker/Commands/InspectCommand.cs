using System.CommandLine;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Cli.Commands;

/// <summary>
/// <c>redecker inspect</c>: reads a package version, and optionally compares it against the version
/// currently referenced, without touching any project file.
/// </summary>
public static class InspectCommand
{
    /// <summary>Builds the command.</summary>
    public static Command Create()
    {
        var packageArgument = new Argument<string>("package")
        {
            Description = "Package identifier to inspect. Omit when using --file.",
            // Optional, because --file names the package by pointing at it. Left required, the
            // parser rejects `inspect --file x.nupkg` before the command ever runs.
            Arity = ArgumentArity.ZeroOrOne,
        };

        // Not Required: --file is the other way to name a package, and demanding --to alongside
        // it would mean repeating a version that is already inside the nupkg.
        var toOption = new Option<string?>("--to")
        {
            Description = "The version being considered. Required unless --file is given.",
        };

        var fileOption = new Option<string?>("--file")
        {
            Description =
                "Inspect a .nupkg on disk instead of downloading one. Identity is read from the " +
                "nuspec inside. Use this to check a package before publishing it.",
        };

        var fromOption = new Option<string?>("--from")
        {
            Description = "The version currently referenced. Enables upgrade-only checks.",
        };

        var sourceOption = new Option<string>("--source")
        {
            Description = "NuGet V3 flat container base URL.",
            DefaultValueFactory = _ => FlatContainerPackageStore.NuGetOrg,
        };

        var command = new Command("inspect", "Check a package version for problems that restore cannot see.")
        {
            packageArgument,
            toOption,
            fromOption,
            sourceOption,
            fileOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileOption);
            if (file is not null)
            {
                return RunFile(file);
            }

            var id = parseResult.GetValue(packageArgument);
            var to = parseResult.GetValue(toOption);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(to))
            {
                Console.Error.WriteLine(
                    "error: give a package and --to, or --file <path.nupkg>.");
                return 2;
            }

            var from = parseResult.GetValue(fromOption);
            var source = parseResult.GetRequiredValue(sourceOption);

            using var store = new FlatContainerPackageStore(source, CacheDirectory());
            return await RunAsync(store, id, from, to, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Checks a package already on disk. Only the rules that need one version can run: an
    /// upgrade comparison has nothing to compare against here.
    /// </summary>
    internal static int RunFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"error: no such file: {path}");
            return 2;
        }

        using var package = PackageArchive.OpenFile(path);
        return Report.Write(SinglePackageRules(package), package.Moniker);
    }

    /// <summary>Every rule that needs only one version of a package.</summary>
    internal static List<Finding> SinglePackageRules(PackageArchive package)
    {
        IPackageRule[] rules =
        [
            new DanglingAssetRule(),
            new ToolPackageRule(),
            new UnimportableBuildFolderRule(),
        ];

        return rules.SelectMany(r => r.Inspect(package)).ToList();
    }

    internal static async Task<int> RunAsync(
        IPackageStore store,
        string id,
        string? from,
        string to,
        CancellationToken cancellationToken)
    {
        using var candidate = await store.GetAsync(id, to, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            Console.Error.WriteLine($"error: {id}@{to} was not found on the configured source.");
            return 2;
        }

        var findings = SinglePackageRules(candidate);

        if (from is not null)
        {
            using var current = await store.GetAsync(id, from, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                Console.Error.WriteLine($"error: {id}@{from} was not found on the configured source.");
                return 2;
            }

            findings.AddRange(new AssetLossRule().Compare(current, candidate));
        }

        return Report.Write(findings, $"{id}@{to}");
    }

    internal static string CacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".redecker-cache");
}
