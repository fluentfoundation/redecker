using System.CommandLine;
using Ratchet.Findings;
using Ratchet.Packages;
using Ratchet.Rules;

namespace Ratchet.Cli.Commands;

/// <summary>
/// <c>ratchet inspect</c>: reads a package version, and optionally compares it against the version
/// currently referenced, without touching any project file.
/// </summary>
public static class InspectCommand
{
    /// <summary>Builds the command.</summary>
    public static Command Create()
    {
        var packageArgument = new Argument<string>("package")
        {
            Description = "Package identifier to inspect.",
        };

        var toOption = new Option<string>("--to")
        {
            Description = "The version being considered.",
            Required = true,
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
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetRequiredValue(packageArgument);
            var to = parseResult.GetRequiredValue(toOption);
            var from = parseResult.GetValue(fromOption);
            var source = parseResult.GetRequiredValue(sourceOption);

            using var store = new FlatContainerPackageStore(source, CacheDirectory());
            return await RunAsync(store, id, from, to, cancellationToken).ConfigureAwait(false);
        });

        return command;
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

        var findings = new List<Finding>();
        findings.AddRange(new DanglingAssetRule().Inspect(candidate));

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
            ".ratchet-cache");
}
