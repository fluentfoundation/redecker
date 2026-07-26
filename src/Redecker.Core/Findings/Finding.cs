namespace Redecker.Findings;

/// <summary>How much a <see cref="Finding"/> should matter to the caller.</summary>
public enum FindingSeverity
{
    /// <summary>Worth knowing, but not a reason to hold an upgrade back.</summary>
    Info,

    /// <summary>Likely to change behaviour; a human should look before taking the upgrade.</summary>
    Warning,

    /// <summary>Known to break the build or drop a platform the consumer targets.</summary>
    Error,
}

/// <summary>
/// One observation about a package or an upgrade. Findings are the only thing rules produce, so
/// they carry enough context to be rendered on a console, serialised to JSON, or turned into a
/// pull request comment without the renderer needing to understand the rule that raised them.
/// </summary>
/// <param name="Code">Stable identifier, e.g. <c>RDK0001</c>, so findings can be suppressed.</param>
/// <param name="Severity">How much this should matter.</param>
/// <param name="Title">One line, suitable for a list.</param>
/// <param name="Detail">The evidence: what was looked at and what was found.</param>
/// <param name="Subject">The package (and version) the finding is about.</param>
public sealed record Finding(
    string Code,
    FindingSeverity Severity,
    string Title,
    string Detail,
    string? Subject = null)
{
    /// <summary>Renders the finding as a single console line.</summary>
    public override string ToString()
    {
        var subject = Subject is null ? "" : $" [{Subject}]";
        return $"{Severity.ToString().ToLowerInvariant()} {Code}{subject}: {Title}";
    }
}
