using Ratchet.Findings;

namespace Ratchet.Cli.Commands;

/// <summary>Console rendering for findings.</summary>
internal static class Report
{
    /// <summary>
    /// Prints findings and returns the process exit code: non-zero when anything is an error, so
    /// the tool can be used directly as a gate in a workflow step.
    /// </summary>
    public static int Write(IReadOnlyCollection<Finding> findings, string subject)
    {
        if (findings.Count == 0)
        {
            Console.WriteLine($"{subject}: no findings.");
            return 0;
        }

        foreach (var finding in findings.OrderByDescending(f => f.Severity))
        {
            var colour = finding.Severity switch
            {
                FindingSeverity.Error => ConsoleColor.Red,
                FindingSeverity.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.DarkGray,
            };

            Write(colour, $"{finding.Severity.ToString().ToLowerInvariant()} {finding.Code}");
            Console.WriteLine($": {finding.Title}");
            Console.WriteLine($"    {finding.Detail}");
            Console.WriteLine();
        }

        var errors = findings.Count(f => f.Severity == FindingSeverity.Error);
        var warnings = findings.Count(f => f.Severity == FindingSeverity.Warning);
        Console.WriteLine($"{subject}: {errors} error(s), {warnings} warning(s).");
        return errors > 0 ? 1 : 0;
    }

    private static void Write(ConsoleColor colour, string text)
    {
        // Console redirection makes colour meaningless and, worse, leaves escape codes in logs.
        if (Console.IsOutputRedirected)
        {
            Console.Write(text);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = colour;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }
}
