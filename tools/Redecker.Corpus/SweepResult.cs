using System.Text.Json;
using System.Text.Json.Serialization;

namespace Redecker.Corpus;

/// <summary>One finding against one package version.</summary>
/// <param name="Code">The rule that fired.</param>
/// <param name="Severity">How much it matters.</param>
/// <param name="Title">One-line description.</param>
public sealed record RecordedFinding(string Code, string Severity, string Title);

/// <summary>What one package version produced.</summary>
/// <param name="Id">Package identifier.</param>
/// <param name="Version">The exact version examined.</param>
/// <param name="Findings">Everything the rules reported; empty means clean.</param>
public sealed record PackageResult(string Id, string Version, IReadOnlyList<RecordedFinding> Findings);

/// <summary>
/// The full result of a sweep, written to disk so runs can be compared.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately free of a timestamp. Identical results should produce an identical file, so that
/// git shows a diff only when a rule's behaviour actually changed — which is the entire reason to
/// keep these. Run metadata that does change every time lives in the Markdown summary instead.
/// </para>
/// <para>
/// Every package examined is recorded, not only the ones that produced findings. Knowing that a
/// package went from clean to flagged is the point; a findings-only file cannot tell you whether
/// silence means "checked and fine" or "never checked".
/// </para>
/// </remarks>
/// <param name="Corpus">How the package list was chosen.</param>
/// <param name="Requested">How many packages were asked for.</param>
/// <param name="Examined">How many were successfully read.</param>
/// <param name="Skipped">How many could not be read.</param>
/// <param name="Rules">The rules that ran, so an empty result is interpretable.</param>
/// <param name="Packages">Every package examined, ordered by id.</param>
public sealed record SweepResult(
    string Corpus,
    int Requested,
    int Examined,
    int Skipped,
    IReadOnlyList<string> Rules,
    IReadOnlyList<PackageResult> Packages)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Findings grouped by rule, ordered for stable output.</summary>
    public IEnumerable<(string Code, List<PackageResult> Packages)> ByRule() =>
        Rules.Select(code => (
                Code: code,
                Packages: Packages
                    .Where(p => p.Findings.Any(f => f.Code == code))
                    .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .Where(x => x.Packages.Count > 0);

    /// <summary>Writes the machine-readable baseline and the human summary.</summary>
    public void Write(string directory)
    {
        Directory.CreateDirectory(directory);

        var stem = Path.Combine(directory, $"top-{Requested}");
        File.WriteAllText($"{stem}.json", JsonSerializer.Serialize(this, Options) + Environment.NewLine);
        File.WriteAllText($"{stem}.md", Summary());

        Console.WriteLine($"Wrote {stem}.json and {stem}.md");
    }

    private string Summary()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"# Corpus sweep: top {Requested}");
        text.AppendLine();
        text.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC from {Corpus}.");
        text.AppendLine();
        text.AppendLine($"Examined **{Examined}** packages, skipped {Skipped}.");
        text.AppendLine();
        text.AppendLine("| Rule | Packages | Rate | Reading |");
        text.AppendLine("| --- | ---: | ---: | --- |");

        foreach (var code in Rules)
        {
            var hits = Packages.Count(p => p.Findings.Any(f => f.Code == code));
            var rate = Examined == 0 ? 0 : 100.0 * hits / Examined;
            text.AppendLine($"| {code} | {hits} | {rate:F1}% | {Reading(rate)} |");
        }

        foreach (var (code, packages) in ByRule())
        {
            text.AppendLine();
            text.AppendLine($"## {code}");
            text.AppendLine();
            foreach (var package in packages)
            {
                foreach (var finding in package.Findings.Where(f => f.Code == code))
                {
                    text.AppendLine($"- `{package.Id}@{package.Version}` — {finding.Title}");
                }
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// A rate is not self-explanatory. Firing on a fifth of widely-used packages says more about
    /// the rule than about the ecosystem, and the report should say so rather than leave a large
    /// number looking like a large discovery.
    /// </summary>
    internal static string Reading(double rate) => rate switch
    {
        > 20 => "**suspect** — too common to be a real defect",
        > 5 => "review — unusually common",
        > 0 => "plausible",
        _ => "no findings",
    };
}
