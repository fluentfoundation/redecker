using System.CommandLine;
using Redecker.Cli.Commands;

namespace Redecker.Cli;

/// <summary>Entry point for the <c>redecker</c> global tool.</summary>
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
        root.Add(CheckCommand.Create());
        root.Add(HintsCommand.Create());

        return root.Parse(args).InvokeAsync();
    }
}
