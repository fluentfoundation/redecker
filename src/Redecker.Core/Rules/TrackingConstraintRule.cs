using NuGet.Versioning;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Projects;

namespace Redecker.Rules;

/// <summary>
/// Reports a package left behind by a version bump to something it declares a range on — a database
/// provider still on last year's release while the core package moved a major version.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LockstepFamilyRule"/> covers packages that must carry the <em>same</em> version. This
/// covers two independently versioned families that must move <em>together</em>, which neither
/// lockstep nor framework banding can express. EF Core states it directly: "check that external
/// database provider supports the version of EF Core you are using. New major versions of EF Core
/// usually require an updated database provider."
/// </para>
/// <para>
/// <b>Nothing here is EF Core specific, and there is no table to maintain.</b> Every provider
/// already declares its own range in its nuspec, so the constraint is read from the ecosystem
/// rather than asserted by Redecker. That generalises for free to ASP.NET Core integration
/// libraries, analyzers tied to a compiler version, and test SDK and adapter pairs.
/// </para>
/// <para>
/// <b>Why this is worth reporting when NuGet already knows.</b> The obvious objection is that
/// restore raises <c>NU1608</c> for exactly this. It does — as a warning, and only sometimes.
/// Measured against real packages:
/// </para>
/// <list type="bullet">
/// <item>Provider constrains one package and the pin is above its range: <c>NU1608</c>, a
/// <b>warning</b>. Restore succeeds, the build succeeds, and the program runs.</item>
/// <item>Provider constrains several packages in a family: <c>NU1107</c> as well, an error.</item>
/// <item>The pin is <em>below</em> the range: <c>NU1605</c>, an error by default.</item>
/// </list>
/// <para>
/// So the failure this catches is the one that gets through — a single warning in a repository that
/// already has a warning count, leaving a provider running against a core version it declares it
/// does not support.
/// </para>
/// <para>
/// This is also precisely what a one-package-per-pull-request updater produces. Bumping the core
/// package alone is individually reasonable and collectively wrong; the fix has to be one atomic
/// change touching both families, which is why the finding names the version to move to rather
/// than only the breach.
/// </para>
/// </remarks>
public sealed class TrackingConstraintRule : INamedRule
{
    /// <summary>How many versions back to search for one that admits the pinned version.</summary>
    /// <remarks>
    /// Bounded because each candidate costs a download. A provider more than this many releases
    /// behind a working pairing is not a version bump, it is a migration, and naming a specific
    /// version would be false precision.
    /// </remarks>
    private const int CandidateLimit = 24;

    /// <inheritdoc />
    public string Code => "RDK0011";

    /// <inheritdoc />
    public string Name => "package left behind by a version bump";

    /// <summary>
    /// Checks every declared version against the ranges the other declared packages place on it.
    /// </summary>
    /// <param name="pins">The repository's declared versions.</param>
    /// <param name="store">Where nuspecs are read from.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Finding>> InspectAsync(
        IEnumerable<PackagePin> pins,
        IPackageStore store,
        CancellationToken cancellationToken = default)
    {
        var declared = Declared(pins);
        var findings = new List<Finding>();

        foreach (var (id, version) in declared.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
        {
            var breaches = await BreachesAsync(id, version, declared, store, cancellationToken)
                .ConfigureAwait(false);

            foreach (var breach in breaches)
            {
                var remedy = await SuggestAsync(id, version, breach, store, cancellationToken)
                    .ConfigureAwait(false);

                findings.Add(new Finding(
                    Code,
                    FindingSeverity.Warning,
                    $"{id} {version} does not support {breach.DependencyId} " +
                    $"{breach.ResolvedVersion}",
                    $"{id} {version} declares {breach.DependencyId} {breach.Range.PrettyPrint()}, " +
                    $"but this repository pins {breach.DependencyId} {breach.ResolvedVersion}. " +
                    "Restore reports this as NU1608, a warning — the build succeeds and the " +
                    "mismatch reaches run time. " + remedy,
                    $"{id} {version}"));
            }
        }

        return findings;
    }

    /// <summary>Every package declared with a concrete version, keyed by id.</summary>
    /// <remarks>
    /// A <c>PackageReference</c> with no version is governed by central package management, and the
    /// <c>PackageVersion</c> item carries the real constraint. Taking the first version seen keeps
    /// a repository that declares the same package twice from being reported twice.
    /// </remarks>
    private static Dictionary<string, NuGetVersion> Declared(IEnumerable<PackagePin> pins)
    {
        var declared = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in pins)
        {
            if (pin.Version is null ||
                declared.ContainsKey(pin.PackageId) ||
                !NuGetVersion.TryParse(pin.Version, out var version))
            {
                continue;
            }

            declared[pin.PackageId] = version;
        }

        return declared;
    }

    private sealed record Breach(string DependencyId, VersionRange Range, NuGetVersion ResolvedVersion);

    /// <summary>
    /// The declarations <paramref name="id"/> makes that the repository's own pins violate.
    /// </summary>
    private static async Task<List<Breach>> BreachesAsync(
        string id,
        NuGetVersion version,
        Dictionary<string, NuGetVersion> declared,
        IPackageStore store,
        CancellationToken cancellationToken)
    {
        var breaches = new List<Breach>();

        using var package = await store.GetAsync(id, version.ToNormalizedString(), cancellationToken)
            .ConfigureAwait(false);
        if (package is null)
        {
            return breaches;
        }

        foreach (var group in package.Dependencies().GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!declared.TryGetValue(group.Key, out var pinned))
            {
                continue;
            }

            // Framework groups can declare different ranges, and NuGet applies only the one
            // matching the consuming project. Report only when no group would accept the pin —
            // anything narrower would depend on a target framework this rule cannot see.
            if (group.Any(d => d.Range.Satisfies(pinned)))
            {
                continue;
            }

            breaches.Add(new Breach(group.Key, group.First().Range, pinned));
        }

        return breaches;
    }

    /// <summary>
    /// Finds the newest version of <paramref name="id"/> whose declared range admits the pinned
    /// version, so the finding names a fix rather than only a problem.
    /// </summary>
    /// <remarks>
    /// Only versions <em>newer</em> than <paramref name="current"/> are considered, and the reason
    /// is a real result rather than caution. Asked which Pomelo release accepts EF Core 10.0.0, an
    /// unfiltered search answers 7.0.0 — truthfully, because Pomelo 7 declares an unbounded
    /// minimum and therefore admits anything above it. That is the same missing upper bound that
    /// makes restore silent, and recommending a downgrade on the strength of it would be worse
    /// than saying nothing.
    /// </remarks>
    private static async Task<string> SuggestAsync(
        string id,
        NuGetVersion current,
        Breach breach,
        IPackageStore store,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> versions;
        try
        {
            versions = await store.GetVersionsAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return $"Move {id} to a version that supports {breach.DependencyId} " +
                   $"{breach.ResolvedVersion}, or hold {breach.DependencyId} back until one exists.";
        }

        var candidates = versions
            .Select(v => NuGetVersion.TryParse(v, out var parsed) ? parsed : null)
            .Where(v => v is not null && !v.IsPrerelease && v > current)
            .Select(v => v!)
            .OrderByDescending(v => v)
            .Take(CandidateLimit)
            .ToList();

        if (candidates.Count == 0)
        {
            return $"No release of {id} is newer than {current.ToNormalizedString()}, so there is " +
                   $"nothing to move up to. Hold {breach.DependencyId} at a version " +
                   $"{id} supports until one ships.";
        }

        foreach (var candidate in candidates)
        {
            using var package = await store
                .GetAsync(id, candidate.ToNormalizedString(), cancellationToken)
                .ConfigureAwait(false);
            if (package is null)
            {
                continue;
            }

            var ranges = package.Dependencies()
                .Where(d => string.Equals(d.Id, breach.DependencyId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // A version that stopped declaring the dependency altogether is not an upgrade path
            // to recommend blindly; only a version that positively admits the pin counts.
            if (ranges.Count > 0 && ranges.Any(r => r.Range.Satisfies(breach.ResolvedVersion)))
            {
                return $"{id} {candidate.ToNormalizedString()} is the newest release that accepts " +
                       $"{breach.DependencyId} {breach.ResolvedVersion}. Move both in one change: " +
                       "bumping either alone leaves the pair broken.";
            }
        }

        return $"No release of {id} newer than {current.ToNormalizedString()} accepts " +
               $"{breach.DependencyId} {breach.ResolvedVersion} either — {candidates.Count} " +
               $"checked. Either hold {breach.DependencyId} back, or treat replacing {id} as a " +
               "migration rather than an upgrade.";
    }
}
