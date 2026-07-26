# Redecker

An update tool for .NET dependencies that reads packages, not just their version graph — and
treats *the reason a pin exists* as machine-readable data with an expiry check.

Named after the German brush and broom makers, because the job is sweeping stale dependencies out
of a repository — and knowing which dust is load-bearing.

> **Status:** early. Two rules and two commands work end to end and are covered by tests against
> the real packages. The roadmap below is honest about what is not built yet.

## Why

Dependabot resolves versions. Its success criterion is *does restore succeed*. That is not enough
for .NET, and here is a case from a real repository that proves it.

`SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 → 2.1.12 clears a security advisory. It restores cleanly,
resolves cleanly, and builds cleanly on every target except `net48` on Windows, where it fails:

```
error MSB3030: Could not copy the file ".../sqlitepclraw.lib.e_sqlite3/2.1.12/runtimes/win-arm/native/e_sqlite3.dll"
because it was not found.
```

2.1.12 stopped shipping `runtimes/win-arm/native/e_sqlite3.dll`, but its
`buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets` still lists that file for copying.
Nothing about this is expressed in the dependency graph, so no amount of version reasoning finds
it. Reading the package finds it in about a second:

```console
$ redecker inspect SQLitePCLRaw.lib.e_sqlite3 --from 2.1.11 --to 2.1.12
error RDK0001: buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets references
    runtimes/win-arm/native/e_sqlite3.dll, which the package does not contain
warning RDK0002: 2.1.11 to 2.1.12 drops 5 runtime identifiers:
    win-arm, win10-arm, win10-arm64, win10-x64, win10-x86
SQLitePCLRaw.lib.e_sqlite3@2.1.12: 1 error(s), 1 warning(s)
```

Exit code 1. That is a CI round, a red pull request, and a revert commit that never had to happen.

## Pin hints

The second idea. When you hold a package back, the reason lives in a comment that no tool can act
on, so the pin outlives its cause and nobody dares remove it.

A hint records the reason **and the condition under which it stops applying**, on the MSBuild
`Label` attribute — a plain attribute NuGet ignores, so no schema changes:

```xml
<PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.11"
                Label="upstream-bug: #:package SQLitePCLRaw.bundle_e_sqlite3@2.1.11;
                       until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12);
                       note: 2.1.12 stopped shipping the win-arm native asset its targets copies" />
```

The subject reuses the `#:package Id@Version` directive syntax from file-based apps. The grammar is:

```
<kind>: #:package <Id>[@<Version>][; until: <condition>][; note: <text>]
```

| Kind | Means | Retires when |
| --- | --- | --- |
| `security-floor` | Explicit reference only to lift a vulnerable transitive floor | the parent raises its own floor |
| `upstream-bug` | A newer version is broken | upstream fixes it |
| `framework-band` | Tied to a TFM's in-box band | never — recomputed per TFM |
| `api-compat` | Avoiding a breaking change | human review |
| `transitive-conflict` | Settling a version conflict | the conflict resolves |

`redecker hints --check` re-evaluates each condition and tells you which pins can now be deleted:

```console
$ redecker hints Directory.Packages.props --check
Directory.Packages.props:3 SQLitePCLRaw.bundle_e_sqlite3 2.1.11
    kind: UpstreamBug
    until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12)
    status: StillRequired - 2.1.12 still has 1 dangling asset reference(s)
```

When upstream fixes the package, the same command says `Retirable` and exits non-zero. The pin
tells you when to remove it instead of becoming folklore.

## The framework band problem

A generic "bump to latest" is wrong for packages that ship with the runtime. A project targeting
`net8.0` wants `Microsoft.Extensions.*` 8.0.x; the same package at 9.0.x raises the floor every
consumer must meet. The update unit is `(package, target framework band)`, not `(package)` —
`FrameworkBand.HighestInBand` implements that, and returns nothing rather than jumping bands when
a band has no release.

## Commands

| Command | Does |
| --- | --- |
| `redecker inspect <id> --to <ver> [--from <ver>]` | Read-only package checks. Exit 1 on any error finding. |
| `redecker hints <path> [--check]` | List pin rationales; re-evaluate exit conditions. |

## Rules

| Code | Severity | Checks |
| --- | --- | --- |
| `RDK0001` | error | Package MSBuild files reference files the package does not ship |
| `RDK0002` | warning | An upgrade drops a `lib/` framework or a `runtimes/` RID |

`RDK0001` only reports paths it can resolve with certainty — references holding an unexpanded
property, item metadata, or a wildcard are skipped. That keeps it usable as a gate: a finding
means a file really is missing.

## Not built yet

- **`redecker plan`** — the read-only update proposal. `dotnet package update` has no `--dry-run`
  (verified against SDK 10.0.302: its only options are `--vulnerable`, `--project`, `--interactive`,
  `--verbosity`), so the plan has to be computed rather than delegated.
- **`transitive-floor` and `advisory-clear` evaluation** — both need a restored graph and the
  advisory database. Today they report `Undetermined` rather than guessing.
- **NuGet.config source support** — `--source` takes a single V3 flat container.
- **Matrix build verification** — the tier that would have caught the SQLite break even without a
  package rule, by building every TFM × OS rather than only restoring.
- **GitHub Action packaging.**

## Building

```console
dotnet build Redecker.slnx -c Release
dotnet test  Redecker.slnx -c Release --filter "TestCategory!=Network"   # offline
dotnet test  Redecker.slnx -c Release --filter "TestCategory=Network"    # hits nuget.org
```

The network tests assert against the real SQLitePCLRaw packages, so the motivating bug stays a
regression test rather than a story in a README.

## License

MIT.
