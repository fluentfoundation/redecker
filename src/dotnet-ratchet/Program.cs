using System.CommandLine;
using Ratchet.Cli.Commands;

namespace Ratchet.Cli;

/// <summary>Entry point for the <c>ratchet</c> global tool.</summary>
public static class Program
{
    /// <summary>Parses the command line and runs the requested command.</summary>
    /// <param name="args">Raw process arguments.</param>
    /// <returns>Zero on success; non-zero when a command reports findings or fails.</returns>
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand(
            "Inspect and update .NET dependencies with knowledge of how NuGet packages are built.");

        root.Add(InspectCommand.Create());
        root.Add(HintsCommand.Create());

        return root.Parse(args).InvokeAsync();
    }
}
