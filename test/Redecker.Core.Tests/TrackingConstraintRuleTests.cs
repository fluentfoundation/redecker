using NUnit.Framework;
using Redecker.Findings;
using Redecker.Packages;
using Redecker.Projects;
using Redecker.Rules;

namespace Redecker.Tests;

[TestFixture]
public class TrackingConstraintRuleTests
{
    /// <summary>A store built from declarations rather than files, so the rule runs offline.</summary>
    private sealed class FakeStore : IPackageStore
    {
        private readonly Dictionary<(string Id, string Version), string> _dependencies =
            new(new Comparer());

        private sealed class Comparer : IEqualityComparer<(string Id, string Version)>
        {
            public bool Equals((string Id, string Version) x, (string Id, string Version) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Version, y.Version);

            public int GetHashCode((string Id, string Version) obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version));
        }

        /// <summary>Declares that <paramref name="id"/> depends on the given ranges.</summary>
        /// <param name="dependencies">Pairs of package id and version range.</param>
        public FakeStore With(string id, string version, params string[] dependencies)
        {
            var items = string.Join(
                "",
                dependencies.Chunk(2).Select(p => $"""<dependency id="{p[0]}" version="{p[1]}" />"""));

            _dependencies[(id, version)] = $"""
                <?xml version="1.0"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata>
                    <id>{id}</id>
                    <version>{version}</version>
                    <dependencies><group targetFramework="net8.0">{items}</group></dependencies>
                  </metadata>
                </package>
                """;
            return this;
        }

        public Task<PackageArchive?> GetAsync(string id, string version, CancellationToken _)
        {
            if (!_dependencies.TryGetValue((id, version), out var nuspec))
            {
                return Task.FromResult<PackageArchive?>(null);
            }

            var package = new SyntheticPackage().With($"{id}.nuspec", nuspec).Build(id, version);
            return Task.FromResult<PackageArchive?>(package);
        }

        public Task<IReadOnlyList<string>> GetVersionsAsync(string id, CancellationToken _) =>
            Task.FromResult<IReadOnlyList<string>>(
                _dependencies.Keys
                    .Where(k => StringComparer.OrdinalIgnoreCase.Equals(k.Id, id))
                    .Select(k => k.Version)
                    .ToList());

        public Task<PackageArchive?> GetSymbolsAsync(string id, string version, CancellationToken _) =>
            Task.FromResult<PackageArchive?>(null);
    }

    private static PackagePin Pin(string id, string? version) =>
        new(id, version, "Directory.Packages.props", 1, "PackageVersion", null, null, null, null);

    private static Task<IReadOnlyList<Finding>> Inspect(FakeStore store, params PackagePin[] pins) =>
        new TrackingConstraintRule().InspectAsync(pins, store, CancellationToken.None);

    // The exact pairing measured against nuget.org: Pomelo 9.0.0 declares
    // Microsoft.EntityFrameworkCore.Relational [9.0.0, 9.0.999] and restore lets 10.0.0 through
    // with nothing but an NU1608 warning.
    [Test]
    public async Task Reports_a_provider_left_behind_by_a_core_bump()
    {
        var store = new FakeStore()
            .With("Pomelo.EntityFrameworkCore.MySql", "9.0.0",
                "Microsoft.EntityFrameworkCore.Relational", "[9.0.0, 9.0.999]");

        var findings = await Inspect(
            store,
            Pin("Pomelo.EntityFrameworkCore.MySql", "9.0.0"),
            Pin("Microsoft.EntityFrameworkCore.Relational", "10.0.0"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Code, Is.EqualTo("RDK0011"));
        Assert.That(findings[0].Title, Does.Contain("does not support"));
        Assert.That(findings[0].Detail, Does.Contain("NU1608"));
    }

    [Test]
    public async Task Names_the_version_that_would_fix_it()
    {
        var store = new FakeStore()
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)")
            .With("Contoso.Provider", "10.0.0", "Contoso.Core", "[10.0.0, 11.0.0)")
            .With("Contoso.Provider", "10.1.0", "Contoso.Core", "[10.0.0, 11.0.0)");

        var findings = await Inspect(
            store, Pin("Contoso.Provider", "9.0.0"), Pin("Contoso.Core", "10.0.0"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(
            findings[0].Detail, Does.Contain("Contoso.Provider 10.1.0"),
            "the newest release that accepts the pin, not merely the first");
        Assert.That(findings[0].Detail, Does.Contain("in one change"));
    }

    [Test]
    public async Task Says_so_when_nothing_newer_exists_at_all()
    {
        var store = new FakeStore()
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)");

        var findings = await Inspect(
            store, Pin("Contoso.Provider", "9.0.0"), Pin("Contoso.Core", "10.0.0"));

        Assert.That(findings[0].Detail, Does.Contain("nothing to move up to"));
    }

    [Test]
    public async Task Says_so_when_newer_releases_exist_but_none_accept_the_pin()
    {
        var store = new FakeStore()
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)")
            .With("Contoso.Provider", "9.1.0", "Contoso.Core", "[9.0.0, 10.0.0)");

        var findings = await Inspect(
            store, Pin("Contoso.Provider", "9.0.0"), Pin("Contoso.Core", "10.0.0"));

        Assert.That(findings[0].Detail, Does.Contain("No release"));
        Assert.That(findings[0].Detail, Does.Contain("migration rather than an upgrade"));
    }

    // Asked which Pomelo release accepts EF Core 10.0.0, an unfiltered search answers 7.0.0 —
    // truthfully, because Pomelo 7 declares an unbounded minimum. Recommending a downgrade on the
    // strength of a missing upper bound would be worse than saying nothing.
    [Test]
    public async Task Never_suggests_moving_the_package_backwards()
    {
        var store = new FakeStore()
            .With("Contoso.Provider", "7.0.0", "Contoso.Core", "7.0.0")
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)");

        var findings = await Inspect(
            store, Pin("Contoso.Provider", "9.0.0"), Pin("Contoso.Core", "10.0.0"));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Detail, Does.Not.Contain("7.0.0"));
        Assert.That(findings[0].Detail, Does.Contain("nothing to move up to"));
    }

    [Test]
    public async Task Stays_silent_when_the_pin_is_inside_the_declared_range()
    {
        var store = new FakeStore()
            .With("Npgsql.EntityFrameworkCore.PostgreSQL", "9.0.4",
                "Microsoft.EntityFrameworkCore.Relational", "[9.0.1, 10.0.0)");

        Assert.That(
            await Inspect(
                store,
                Pin("Npgsql.EntityFrameworkCore.PostgreSQL", "9.0.4"),
                Pin("Microsoft.EntityFrameworkCore.Relational", "9.0.4")),
            Is.Empty);
    }

    // Npgsql 8.0.11 declares "8.0.11", a minimum with no upper bound. Restore raised nothing at
    // all for this pairing, and neither should the rule.
    [Test]
    public async Task Treats_a_bare_version_as_a_minimum_not_an_exact_match()
    {
        var store = new FakeStore()
            .With("Npgsql.EntityFrameworkCore.PostgreSQL", "8.0.11",
                "Microsoft.EntityFrameworkCore.Relational", "8.0.11");

        Assert.That(
            await Inspect(
                store,
                Pin("Npgsql.EntityFrameworkCore.PostgreSQL", "8.0.11"),
                Pin("Microsoft.EntityFrameworkCore.Relational", "9.0.0")),
            Is.Empty);
    }

    [Test]
    public async Task Ignores_dependencies_the_repository_does_not_declare_itself()
    {
        // The constraint is real, but nothing here pins the dependency, so restore picks a version
        // that satisfies it and there is nothing to report.
        var store = new FakeStore()
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)");

        Assert.That(await Inspect(store, Pin("Contoso.Provider", "9.0.0")), Is.Empty);
    }

    [Test]
    public async Task Ignores_a_reference_with_no_version_of_its_own()
    {
        // Governed by central package management; the PackageVersion item carries the constraint.
        var store = new FakeStore()
            .With("Contoso.Provider", "9.0.0", "Contoso.Core", "[9.0.0, 10.0.0)");

        Assert.That(
            await Inspect(store, Pin("Contoso.Provider", null), Pin("Contoso.Core", "10.0.0")),
            Is.Empty);
    }

    [Test]
    public async Task Survives_a_package_that_is_not_on_the_feed()
    {
        var findings = await Inspect(
            new FakeStore(), Pin("Contoso.Provider", "9.0.0"), Pin("Contoso.Core", "10.0.0"));

        Assert.That(findings, Is.Empty, "an unresolvable package is not evidence of a breach");
    }

    [Test]
    public void Is_reviewable_by_name()
    {
        var rule = new TrackingConstraintRule();

        Assert.Multiple(() =>
        {
            Assert.That(rule.Code, Is.EqualTo("RDK0011"));
            Assert.That(rule.Name, Is.EqualTo("package left behind by a version bump"));
        });
    }
}
