using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Redecker.Hints;

/// <summary>
/// Parses the pin-rationale grammar carried on an MSBuild <c>Label</c> attribute.
/// </summary>
/// <remarks>
/// <para>The grammar is deliberately one line, because it has to live inside an XML attribute:</para>
/// <code>
/// &lt;kind&gt;: #:package &lt;Id&gt;[@&lt;Version&gt;][; until: &lt;condition&gt;][; note: &lt;text&gt;]
/// </code>
/// <para>
/// The subject reuses the <c>#:package</c> directive syntax from file-based apps so that the same
/// spelling of "this package at this version" appears in both places.
/// </para>
/// <para>
/// <c>Label</c> is a plain MSBuild attribute that NuGet ignores, which makes it a free carrier
/// with no schema change. The one caveat worth knowing before adopting it widely is that the SDK
/// rewrites these item elements when <c>dotnet package add</c> or <c>dotnet package update</c>
/// touches them, and it has no reason to preserve an attribute it does not know about.
/// </para>
/// </remarks>
public static partial class HintParser
{
    [GeneratedRegex(@"^\s*#:package\s+(?<id>[^\s@]+)(?:@(?<version>\S+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectPattern();

    [GeneratedRegex(@"^(?<fn>[a-z-]+)\s*\(\s*(?<arg>[^)]*)\s*\)\s*(?:(?<op>>=|=)\s*(?<rhs>\S+))?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConditionPattern();

    private static readonly Dictionary<string, HintKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["security-floor"] = HintKind.SecurityFloor,
        ["framework-band"] = HintKind.FrameworkBand,
        ["upstream-bug"] = HintKind.UpstreamBug,
        ["api-compat"] = HintKind.ApiCompat,
        ["transitive-conflict"] = HintKind.TransitiveConflict,
    };

    /// <summary>
    /// Attempts to parse a label. Returns <see langword="false"/> without an error for labels that
    /// are not hints at all, since <c>Label</c> is used for ordinary grouping too.
    /// </summary>
    /// <param name="label">The raw attribute value.</param>
    /// <param name="hint">The parsed hint.</param>
    /// <param name="error">Why a label that looked like a hint could not be parsed.</param>
    public static bool TryParse(
        string? label,
        [NotNullWhen(true)] out Hint? hint,
        out string? error)
    {
        hint = null;
        error = null;

        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var colon = label.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        var kindText = label[..colon].Trim();
        if (!Kinds.TryGetValue(kindText, out var kind))
        {
            // Not one of ours. A label like Label="test" is perfectly normal grouping.
            return false;
        }

        var segments = SplitSegments(label[(colon + 1)..]);
        if (segments.Count == 0)
        {
            error = $"Hint '{kindText}' has no subject; expected '#:package <Id>[@<Version>]'.";
            return false;
        }

        var subject = SubjectPattern().Match(segments[0]);
        if (!subject.Success)
        {
            error = $"Hint subject '{segments[0].Trim()}' is not of the form '#:package <Id>[@<Version>]'.";
            return false;
        }

        ExitCondition? exit = null;
        string? note = null;

        foreach (var segment in segments.Skip(1))
        {
            var (key, value) = SplitKeyValue(segment);
            switch (key)
            {
                case "until":
                    if (!TryParseCondition(value, out exit, out var conditionError))
                    {
                        error = conditionError;
                        return false;
                    }

                    break;
                case "note":
                    note = value;
                    break;
                default:
                    error = $"Unrecognised hint segment '{key}'; expected 'until' or 'note'.";
                    return false;
            }
        }

        hint = new Hint(
            kind,
            subject.Groups["id"].Value,
            subject.Groups["version"].Success ? subject.Groups["version"].Value : null,
            exit,
            note,
            label);
        return true;
    }

    /// <summary>
    /// Splits on semicolons, but only up to the start of a note, so that free text may contain
    /// semicolons without being misread as further segments.
    /// </summary>
    private static List<string> SplitSegments(string body)
    {
        var segments = new List<string>();
        var remaining = body;

        while (true)
        {
            var noteIndex = remaining.IndexOf("note:", StringComparison.OrdinalIgnoreCase);
            var semicolon = remaining.IndexOf(';');

            if (semicolon < 0 || (noteIndex >= 0 && semicolon > noteIndex))
            {
                break;
            }

            segments.Add(remaining[..semicolon]);
            remaining = remaining[(semicolon + 1)..];
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            segments.Add(remaining);
        }

        return segments.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
    }

    private static (string Key, string Value) SplitKeyValue(string segment)
    {
        var colon = segment.IndexOf(':');
        return colon < 0
            ? (segment.Trim().ToLowerInvariant(), string.Empty)
            : (segment[..colon].Trim().ToLowerInvariant(), segment[(colon + 1)..].Trim());
    }

    private static bool TryParseCondition(string text, out ExitCondition? condition, out string? error)
    {
        condition = null;
        error = null;

        switch (text.Trim().ToLowerInvariant())
        {
            case "never":
                condition = new ExitCondition.Never();
                return true;
            case "review":
                condition = new ExitCondition.Review();
                return true;
        }

        var match = ConditionPattern().Match(text);
        if (!match.Success)
        {
            error = $"Exit condition '{text}' is not recognised.";
            return false;
        }

        var function = match.Groups["fn"].Value.ToLowerInvariant();
        var argument = match.Groups["arg"].Value.Trim();

        switch (function)
        {
            case "package-assets-intact":
            {
                var at = argument.LastIndexOf('@');
                if (at <= 0)
                {
                    error = $"package-assets-intact expects '<Id>@<Version>', got '{argument}'.";
                    return false;
                }

                condition = new ExitCondition.PackageAssetsIntact(argument[..at], argument[(at + 1)..]);
                return true;
            }

            case "transitive-floor":
            {
                if (!match.Groups["rhs"].Success)
                {
                    error = "transitive-floor expects a comparison, e.g. 'transitive-floor(X) >= 1.2.3'.";
                    return false;
                }

                condition = new ExitCondition.TransitiveFloor(argument, match.Groups["rhs"].Value);
                return true;
            }

            case "advisory-clear":
                condition = new ExitCondition.AdvisoryClear(argument);
                return true;

            default:
                error = $"Unknown exit condition function '{function}'.";
                return false;
        }
    }
}
