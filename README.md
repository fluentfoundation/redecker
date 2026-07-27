# Redecker

An update tool for .NET dependencies that reads packages, not just their version graph — and
treats *the reason a pin exists* as machine-readable data with an expiry check.

Named after the German brush and broom makers, because the job is sweeping stale dependencies out
of a repository — and knowing which dust is load-bearing.

> **Status:** early. Three rules and two commands work end to end, covered by tests that run
> against the real packages and the real GitHub API. The roadmap below is honest about what is
> not built yet.

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

A label on an `<ItemGroup>` applies to every package inside it, which is the natural place for a
hint covering a whole family:

```xml
<ItemGroup Label="framework-band: #:package Microsoft.EntityFrameworkCore.*; until: never">
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.11" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.11" />
</ItemGroup>
```

### Exit conditions

| Condition | Retires when | Evaluated today |
| --- | --- | --- |
| `package-assets-intact(Id@Version)` | that version stops failing the package rules | ✅ |
| `issues-closed(123, 456)` | every issue is closed **as completed** | ✅ |
| `issues-released(123)` | …and the closing commits have reached a release tag | ✅ |
| `transitive-floor(Id) >= 1.2.3` | a dependant raises its own floor | needs a resolved graph |
| `advisory-clear(GHSA-…)` | the advisory stops applying | needs the advisory database |
| `never` / `review` | structural / human decision | n/a |

### Waiting on upstream issues

```xml
<PackageVersion Include="Some.Package" Version="1.2.3"
                Label="upstream-bug: #:package Some.Package@1.2.3;
                       until: issues-released(1234, 1235);
                       note: crashes on net48 when the RID graph is trimmed" />
```

The repository is **not** named. It comes from the pinned package's own nuspec
(`<repository url="...">`, falling back to `projectUrl` when that points at a source host), so a
hint only states which issues it waits on — and it stays correct if the project moves, because
the URL is read from whichever version is pinned.

Two deliberate distinctions:

- **Closed as *not planned* does not discharge a pin.** The tracker is tidy, but upstream has
  declined to fix the defect, so the pin is still earning its place. Only *closed as completed*
  counts.
- **`issues-closed` is not `issues-released`.** A fix merged to `main` is not a fix you can
  consume. `issues-released` additionally requires the closing commit to appear in a release tag,
  and reports the milestone when one is assigned.

#### This does not clone anything

`git tag --contains` needs the commit graph, but the REST compare endpoint answers the same
question directly: comparing a tag against a commit returns `identical` or `behind` when the tag
contains it, and `ahead` when it does not. Containment is monotonic along an ordered release
history, so the *earliest* containing tag is found by binary search over version-sorted tags —
on a repository with 72 tags that is **8 requests instead of 73**, and no clone at all.

For scale: an authenticated user gets 5,000 REST requests an hour, and a workflow's
`GITHUB_TOKEN` gets 1,000 per hour per repository. A pin waiting on three issues costs roughly a
dozen requests. Pass `--github-token`, or set `GITHUB_TOKEN`; without one GitHub allows 60 an
hour, which is not enough from a shared CI address.

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

A generic "bump to latest" is wrong for packages tied to a runtime generation. A project
targeting `net8.0` wants the 8.x line even when 9.x exists, because 9.x is written against a
runtime it is not running on. The update unit is `(package, target framework band)`, not
`(package)` — and an empty band reports nothing rather than quietly jumping generations.

**Which packages those are is policy, not a prefix.** The tempting shortcut — treat all
`Microsoft.Extensions.*` and `System.*` as banded — is wrong in both directions, so
[`BandPolicy`](src/Redecker.Core/Frameworks/BandPolicy.cs) states it as data you can override.

| Banded | Why |
| --- | --- |
| `Microsoft.EntityFrameworkCore.*` | Providers and tools rely on runtime behaviour exclusive to the generation they ship with |
| `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Shipped outside the shared framework but written against a specific ASP.NET Core |
| `Microsoft.Extensions.Hosting`, `.DependencyInjection`, `.Configuration`, `.Http.Polly` | Pulling a 9.0 extension into a `net8.0` app lifts the assets out of the shared framework and ships them app-local and unoptimised |
| `System.Diagnostics.DiagnosticSource`, `System.Text.Json` | Deep runtime and serialization integration; mismatches surface as missing types or contract differences |

**Not banded**, and deliberately so: most of `Microsoft.Extensions.*` is compile-at-head. Caching,
options, primitives and the abstractions packages support older frameworks through netstandard2.0
and should simply take the newest stable release. Holding them at 8.x achieves nothing.

Note the failure mode throughout: none of these break restore. They ship an unoptimised asset, or
surface a missing type at run time. That is precisely why a version-graph updater cannot see them.

### Lockstep families

A separate constraint, and conflating it with banding loses it. Banding says a package must match
the *target framework*; lockstep says a set of packages must match *each other*, whatever version
that is. EF Core's [package documentation](https://learn.microsoft.com/en-us/ef/core/what-is-new/nuget-packages#package-versions)
states it directly:

> Make sure to install the same version of all EF Core packages shipped by Microsoft. For example,
> if version 5.0.3 of `Microsoft.EntityFrameworkCore.SqlServer` is installed, then all other
> `Microsoft.EntityFrameworkCore.*` packages must also be at 5.0.3.

`RDK0003` reports a split family. This is exactly what an automatic updater creates: it bumps
whichever members happen to have newer releases, splits the set, and restore still succeeds.

```console
error RDK0003: Microsoft.EntityFrameworkCore* packages are split across 2 versions: 9.0.0, 9.0.5
```

The same documentation notes that external providers must be compatible with the EF Core version
in use, and that a new major usually requires an updated provider. That is a *cross-family*
constraint — two independently versioned families that must move together — which neither lockstep
nor banding can express. Tracked in [#1](https://github.com/fluentfoundation/redecker/issues/1).

## Commands

| Command | Does | Network |
| --- | --- | --- |
| `redecker inspect <id> --to <ver> [--from <ver>]` | Read a package version and check it. Exit 1 on any error finding. | yes |
| `redecker check <path>` | Check that the versions a repository declares are coherent. | no |
| `redecker hints <path> [--check]` | List pin rationales; re-evaluate exit conditions. | with `--check` |

## Rules

| Code | Severity | Checks |
| --- | --- | --- |
| `RDK0001` | error | Package MSBuild files reference files the package does not ship |
| `RDK0002` | warning | An upgrade drops a `lib/` framework or a `runtimes/` RID |
| `RDK0003` | error | A lockstep family is split across versions |

`RDK0001` only reports paths it can resolve with certainty — references holding an unexpanded
property, item metadata, or a wildcard are skipped. That keeps it usable as a gate: a finding
means a file really is missing.

## Not built yet

- **`redecker plan`** — the read-only update proposal. `dotnet package update` has no `--dry-run`
  (verified against SDK 10.0.302: its only options are `--vulnerable`, `--project`, `--interactive`,
  `--verbosity`), so the plan has to be computed rather than delegated.
- **`transitive-floor` and `advisory-clear` evaluation** — both need a restored graph and the
  advisory database. Today they report `Undetermined` rather than guessing.
- **Cross-family constraints** — providers that must track another family's version, such as an EF
  Core provider following EF Core's major. [#1](https://github.com/fluentfoundation/redecker/issues/1).
- **NuGet.config source support** — `--source` takes a single V3 flat container.
- **Matrix build verification** — the tier that would have caught the SQLite break even without a
  package rule, by building every TFM × OS rather than only restoring.
- **GitHub Action packaging.**

## Building

```console
dotnet build Redecker.slnx -c Release
dotnet test  Redecker.slnx -c Release --filter "TestCategory!=Network"   # offline
dotnet test  Redecker.slnx -c Release --filter "TestCategory=Network"    # nuget.org + GitHub API
```

The network tests assert against the real SQLitePCLRaw packages and the real GitHub API, so the
motivating bug stays a regression test rather than a story in a README. Set `GITHUB_TOKEN` before
running them: unauthenticated GitHub allows only 60 requests an hour.

## Releasing

Publishing uses [nuget.org Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing):
no API key is stored anywhere. The workflow requests a short-lived OIDC token from GitHub,
nuget.org validates it against a policy, and returns an API key valid for one hour.

### One-time setup

1. **Create the `release` environment** in the repository settings (Settings → Environments).
   Add required reviewers if you want a human gate before any push to nuget.org.

2. **Add a repository variable** `NUGET_USER` (Settings → Secrets and variables → Actions →
   Variables), set to the nuget.org **profile name of whoever creates the policy**.

   > This is the single easiest field to get wrong. It is *not* the policy name, and *not*
   > `fluentfoundation` — an organization can own a policy, but the token exchange
   > authenticates the individual who created it. `NuGet/login` sends this value as
   > `username`, and reports `Make sure you are using the username of the policy creator,
   > not the policy owner` when it is wrong.

3. **Register the policy** at <https://www.nuget.org/account/trustedpublishing>:

   | Field | Value |
   | --- | --- |
   | Policy owner | `fluentfoundation` (the organization) |
   | Repository Owner | `fluentfoundation` |
   | Repository | `redecker` |
   | Workflow File | `release.yaml` |
   | Environment | `release` |

   Note the asymmetry: the policy is **owned** by the organization, but `NUGET_USER` is the
   **creator's** personal profile name. Those are two different values.

   > **Workflow File takes the file name only** — `release.yaml`, *not*
   > `.github/workflows/release.yaml`. The policy is bound to that name, so renaming
   > [`.github/workflows/release.yaml`](.github/workflows/release.yaml) breaks publishing until
   > the policy is updated. Treat the name as part of the published contract.

   > The `Environment` value must match `environment: release` in the workflow. If you leave
   > the field blank, remove that line from the workflow too, or the exchange fails.

### Cutting a release

```console
git tag v0.1.0
git push origin v0.1.0
```

The tag push triggers [`release.yaml`](.github/workflows/release.yaml), which builds, tests
(including against real packages), packs, and publishes. `workflow_dispatch` runs the same job
with `dry-run` defaulted to true, so you can rehearse the whole thing without pushing.

GitVersion computes the version from the tag; see [`GitVersion.yml`](GitVersion.yml), which
follows the same conventions as FluentMigrator.

## License

MIT — see [LICENSE](LICENSE).
