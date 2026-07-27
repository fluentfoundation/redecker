namespace Redecker.Frameworks;

/// <summary>
/// Which packages are tied to a target framework's generation, and which families must move as
/// a unit.
/// </summary>
/// <remarks>
/// <para>
/// This is policy, not physics, which is why it is data rather than a hard-coded prefix test.
/// The tempting shortcut -- treat every <c>Microsoft.Extensions.*</c> and <c>System.*</c>
/// package as banded -- is wrong in both directions. Much of
/// <c>Microsoft.Extensions.*</c> is compile-at-head: caching and options support older target
/// frameworks through netstandard2.0 and can simply take the newest stable release. Meanwhile
/// packages that share neither prefix, such as the ASP.NET Core OpenAPI and EF Core integration
/// libraries, are firmly bound to the runtime generation they ship alongside.
/// </para>
/// <para>
/// The default below is a starting policy, not a law. Repositories differ, and the constructor
/// exists so a project can state its own.
/// </para>
/// </remarks>
public sealed class BandPolicy
{
    /// <summary>Packages whose major version must match the target framework's generation.</summary>
    private readonly HashSet<string> _bandedIds;

    /// <summary>Prefixes where the whole family is banded.</summary>
    private readonly string[] _bandedPrefixes;

    /// <summary>Prefixes whose members must all carry the same version as one another.</summary>
    private readonly string[] _lockstepPrefixes;

    /// <param name="bandedIds">Exact package identifiers that are band-bound.</param>
    /// <param name="bandedPrefixes">Family prefixes that are band-bound.</param>
    /// <param name="lockstepPrefixes">Family prefixes that must share one version.</param>
    public BandPolicy(
        IEnumerable<string>? bandedIds = null,
        IEnumerable<string>? bandedPrefixes = null,
        IEnumerable<string>? lockstepPrefixes = null)
    {
        _bandedIds = new HashSet<string>(bandedIds ?? [], StringComparer.OrdinalIgnoreCase);
        _bandedPrefixes = (bandedPrefixes ?? []).ToArray();
        _lockstepPrefixes = (lockstepPrefixes ?? []).ToArray();
    }

    /// <summary>The default policy, documented in the README.</summary>
    public static BandPolicy Default { get; } = new(
        bandedIds:
        [
            // Shipped outside the shared framework but written against a specific ASP.NET Core
            // generation.
            "Microsoft.AspNetCore.OpenApi",
            "Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore",
            "Microsoft.AspNetCore.Identity.EntityFrameworkCore",

            // Taking a 9.0 extension into a net8.0 app lifts these assets out of the shared
            // framework and ships them app-local and unoptimised. They work, so nothing fails
            // loudly -- which is exactly why it is worth flagging.
            "Microsoft.Extensions.Hosting",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Http.Polly",

            // Deep runtime and serialization integration, where a mismatch shows up as missing
            // types or contract differences rather than a build error.
            "System.Diagnostics.DiagnosticSource",
            "System.Text.Json",
        ],
        bandedPrefixes:
        [
            // EF Core relies on runtime behaviour specific to the generation it ships with.
            "Microsoft.EntityFrameworkCore",
        ],
        lockstepPrefixes:
        [
            // "If version 5.0.3 of Microsoft.EntityFrameworkCore.SqlServer is installed, then
            // all other Microsoft.EntityFrameworkCore.* packages must also be at 5.0.3."
            // https://learn.microsoft.com/en-us/ef/core/what-is-new/nuget-packages#package-versions
            "Microsoft.EntityFrameworkCore",
        ]);

    /// <summary>Whether a package's version must match the target framework's generation.</summary>
    public bool IsBanded(string packageId) =>
        _bandedIds.Contains(packageId) ||
        _bandedPrefixes.Any(p => packageId.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The lockstep family a package belongs to, or <see langword="null"/> when it belongs to
    /// none.
    /// </summary>
    public string? LockstepFamily(string packageId) =>
        _lockstepPrefixes.FirstOrDefault(
            p => packageId.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
