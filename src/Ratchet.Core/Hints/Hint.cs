namespace Ratchet.Hints;

/// <summary>Why a pin exists. The kind determines what would have to change for it to retire.</summary>
public enum HintKind
{
    /// <summary>Unrecognised kind; the hint is carried through but cannot be evaluated.</summary>
    Unknown,

    /// <summary>
    /// An explicit reference that exists only to lift a transitive dependency above a vulnerable
    /// version. Retires when the package that drags it in raises its own floor.
    /// </summary>
    SecurityFloor,

    /// <summary>
    /// Pinned to match the in-box band of a target framework. Never retires; it is recomputed per
    /// target framework instead.
    /// </summary>
    FrameworkBand,

    /// <summary>Held back because a newer version is broken. Retires when upstream fixes it.</summary>
    UpstreamBug,

    /// <summary>Held back to avoid a breaking API change. Retires only by human decision.</summary>
    ApiCompat,

    /// <summary>Pinned to settle a version conflict between two dependants.</summary>
    TransitiveConflict,
}

/// <summary>
/// The condition under which a pin stops being necessary.
/// </summary>
/// <remarks>
/// This is the part that makes the scheme worth having. Recording <em>why</em> a pin exists is
/// only documentation; recording what would have to become true for it to go away lets a tool
/// re-check it on every run and tell you when to delete it. Without that, pins accumulate and
/// nobody dares remove them because nobody remembers what they were protecting against.
/// </remarks>
public abstract record ExitCondition
{
    /// <summary>Retires when the named package version no longer fails the package rules.</summary>
    public sealed record PackageAssetsIntact(string PackageId, string Version) : ExitCondition
    {
        /// <inheritdoc />
        public override string ToString() => $"package-assets-intact({PackageId}@{Version})";
    }

    /// <summary>Retires when the resolved floor of a transitive dependency reaches a version.</summary>
    public sealed record TransitiveFloor(string PackageId, string Version) : ExitCondition
    {
        /// <inheritdoc />
        public override string ToString() => $"transitive-floor({PackageId}) >= {Version}";
    }

    /// <summary>Retires when an advisory no longer applies to the resolved graph.</summary>
    public sealed record AdvisoryClear(string AdvisoryId) : ExitCondition
    {
        /// <inheritdoc />
        public override string ToString() => $"advisory-clear({AdvisoryId})";
    }

    /// <summary>Never retires automatically; it is structural.</summary>
    public sealed record Never : ExitCondition
    {
        /// <inheritdoc />
        public override string ToString() => "never";
    }

    /// <summary>Retires only when a human decides; the tool will not propose removing it.</summary>
    public sealed record Review : ExitCondition
    {
        /// <inheritdoc />
        public override string ToString() => "review";
    }
}

/// <summary>
/// A parsed pin rationale, carried on the MSBuild <c>Label</c> attribute of a
/// <c>PackageVersion</c> or <c>PackageReference</c> item.
/// </summary>
/// <param name="Kind">Why the pin exists.</param>
/// <param name="PackageId">The package the pin is about.</param>
/// <param name="Version">The version pinned to, if the hint states one.</param>
/// <param name="Exit">What would have to be true for the pin to retire.</param>
/// <param name="Note">Free text for the human reading the diff.</param>
/// <param name="Raw">The original label, so it can be rewritten losslessly.</param>
public sealed record Hint(
    HintKind Kind,
    string PackageId,
    string? Version,
    ExitCondition? Exit,
    string? Note,
    string Raw);
