using Redecker.Findings;
using Redecker.Packages;
using Redecker.Rules;

namespace Redecker.Hints;

/// <summary>Whether a pin is still doing a job.</summary>
public enum PinStatus
{
    /// <summary>The exit condition could not be evaluated automatically.</summary>
    Undetermined,

    /// <summary>The condition still holds; the pin is still needed.</summary>
    StillRequired,

    /// <summary>The condition has cleared; the pin can be removed.</summary>
    Retirable,
}

/// <summary>The outcome of evaluating one pin's exit condition.</summary>
/// <param name="Status">Whether the pin can go.</param>
/// <param name="Explanation">What was checked and what was found.</param>
public sealed record PinVerdict(PinStatus Status, string Explanation);

/// <summary>
/// Evaluates exit conditions, turning a recorded rationale into a recurring check.
/// </summary>
/// <param name="store">Where packages are read from.</param>
/// <param name="issues">
/// An upstream issue tracker. Optional, because most exit conditions never need one; issue
/// conditions report Undetermined rather than failing when it is absent.
/// </param>
public sealed class HintEvaluator(IPackageStore store, Redecker.Issues.IIssueTracker? issues = null)
{
    private readonly DanglingAssetRule _dangling = new();

    /// <summary>Evaluates a hint's exit condition.</summary>
    public async Task<PinVerdict> EvaluateAsync(Hint hint, CancellationToken cancellationToken)
    {
        switch (hint.Exit)
        {
            case null:
                return new PinVerdict(
                    PinStatus.Undetermined,
                    "The hint records a reason but no exit condition, so nothing can be re-checked. " +
                    "Add 'until: ...' to make it self-retiring.");

            case ExitCondition.Never:
                return new PinVerdict(PinStatus.StillRequired, "Structural pin; not expected to retire.");

            case ExitCondition.Review:
                return new PinVerdict(PinStatus.Undetermined, "Retires by human review only.");

            case ExitCondition.PackageAssetsIntact intact:
                return await EvaluateAssetsIntactAsync(intact, cancellationToken).ConfigureAwait(false);

            case ExitCondition.TransitiveFloor floor:
                return new PinVerdict(
                    PinStatus.Undetermined,
                    $"Evaluating {floor} needs a restored dependency graph, which this command does " +
                    "not build. Run 'redecker plan' against the project to resolve it.");

            case ExitCondition.AdvisoryClear advisory:
                return new PinVerdict(
                    PinStatus.Undetermined,
                    $"Evaluating {advisory} needs the vulnerability database, which is not wired up yet.");

            case ExitCondition.IssuesResolved issuesResolved:
                return issues is null
                    ? new PinVerdict(
                        PinStatus.Undetermined,
                        $"Evaluating {issuesResolved} needs access to the upstream tracker. Pass " +
                        "--github-token, or set GITHUB_TOKEN, and run 'redecker hints --check'.")
                    : await new IssueConditionEvaluator(store, issues)
                        .EvaluateAsync(hint, issuesResolved, cancellationToken).ConfigureAwait(false);

            default:
                return new PinVerdict(PinStatus.Undetermined, $"Unhandled exit condition '{hint.Exit}'.");
        }
    }

    /// <summary>
    /// The self-retiring case that works end to end today: re-download the version that was
    /// rejected, re-run the package rules, and report whether the reason for the pin has gone.
    /// </summary>
    private async Task<PinVerdict> EvaluateAssetsIntactAsync(
        ExitCondition.PackageAssetsIntact condition,
        CancellationToken cancellationToken)
    {
        using var package = await store
            .GetAsync(condition.PackageId, condition.Version, cancellationToken)
            .ConfigureAwait(false);

        if (package is null)
        {
            return new PinVerdict(
                PinStatus.StillRequired,
                $"{condition.PackageId}@{condition.Version} is not published, so the pin still applies.");
        }

        var findings = _dangling.Inspect(package)
            .Where(f => f.Severity == FindingSeverity.Error)
            .ToList();

        if (findings.Count == 0)
        {
            return new PinVerdict(
                PinStatus.Retirable,
                $"{condition.PackageId}@{condition.Version} no longer has dangling asset references. " +
                "The reason for this pin has gone; remove the pin and take the upgrade.");
        }

        return new PinVerdict(
            PinStatus.StillRequired,
            $"{condition.PackageId}@{condition.Version} still has {findings.Count} dangling asset " +
            $"reference(s), e.g. {findings[0].Title}.");
    }
}
