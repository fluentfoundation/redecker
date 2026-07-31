<img src="assets/redecker-icon-128.png" alt="" width="96" align="right" />

# Redecker

**Green build. Broken package.**

`restore` only checks the maths. It never opens the box. Redecker does — and catches the upgrades
that pass every check you run and fail anyway.

[![dotnet-redecker](https://img.shields.io/nuget/v/dotnet-redecker?label=dotnet-redecker)](https://www.nuget.org/packages/dotnet-redecker)
[![Redecker.MSBuild](https://img.shields.io/nuget/v/Redecker.MSBuild?label=Redecker.MSBuild)](https://www.nuget.org/packages/Redecker.MSBuild)

```console
dotnet tool install --global dotnet-redecker   # investigate an upgrade
dotnet add package Redecker.MSBuild            # fail the build instead
```

📖 **[Full documentation](https://fluentfoundation.github.io/redecker/)** · named after the German
broom makers, because the job is sweeping stale dependencies out — and knowing which dust is
load-bearing.

> **Status:** early. Ten rules and three commands work end to end, tested against real packages
> and the real GitHub API. [Not built yet](#not-built-yet) is honest.

## Thirty seconds

Your tooling asks one question: **does it restore?** Restore is a constraint solver. It reads
version numbers, checks the arithmetic, reports success. It never opens a package.

So this passes:

```console
$ dotnet restore
Restored in 3.2s.
```

And this was actually in the box:

```console
$ redecker inspect SQLitePCLRaw.lib.e_sqlite3 --from 2.1.11 --to 2.1.12
error RDK0001: buildTransitive/net461/…targets references
    runtimes/win-arm/native/e_sqlite3.dll, which the package does not contain
warning RDK0002: drops 5 runtime identifiers: win-arm, win10-arm, win10-arm64, win10-x64, win10-x86
```

That upgrade clears a CVE, builds on every target except `net48` on Windows, and cost a CI round,
a red pull request and a revert commit. Found here in about a second, from metadata.

## Seven problems, all of which restore perfectly

| # | Problem | Bites you | |
| --- | --- | --- | --- |
| 1 | Package points at files it no longer ships | Build time — one TFM, often one OS | [RDK0001](#rules) |
| 2 | An upgrade drops a platform you rely on | On the device, not in CI | [RDK0002](#rules) |
| 3 | A family that must move together gets split | Run time, as a missing type | [RDK0003](#lockstep-families) |
| 4 | A promoted transitive pin nobody dares delete | **Never** — that *is* the problem | [RDK0004](#rdk0004-undocumented-transitive-pins) |
| 5 | A package dragged past its runtime generation | Never, loudly | [Bands](#the-framework-band-problem) |
| 6 | A pin outlives its reason and becomes folklore | Never | [Hints](#pin-hints) |
| 7 | An advisory with no clean upgrade path | When the graph will not resolve | [Hints](#pin-hints) |

**Four of the seven are never reported by anything.** Not because they are rare — because nothing
is looking.

Problem 1 is the least interesting one on the list: a single upstream mistake, fixable by hand.
The other six are structural, and recur.

### RDK0004: undocumented transitive pins

The one most repositories already have, without knowing.

A transitive dependency is an implementation detail of the package you actually chose. It has no
business appearing in `Directory.Packages.props` — until someone floats its floor to get above a
vulnerable version, and it appears there permanently:

```xml
<!-- why is this here? -->
<PackageVersion Include="System.Text.RegularExpressions" Version="4.3.1" />
```

Nothing in the file distinguishes that from an ordinary dependency. Nobody can tell whether
deleting it tidies up or quietly reintroduces a CVE, so nobody touches it — and the day the
parent package raises its own floor and the entry becomes redundant passes unnoticed.

`redecker check` finds them by comparing what is *declared* against what is *referenced*:

```console
warning RDK0004: System.Text.RegularExpressions is given a version but no project references it
    ... either a transitive floor someone raised deliberately, or an entry that has outlived
    whatever needed it. Nothing in the file says which, so nobody can safely delete it.
```

A pin carrying a [hint](#pin-hints) is silent, which is the entire point — the rule asks for a
reason, not for the pin's removal:

```xml
<PackageVersion Include="System.Text.RegularExpressions" Version="4.3.1"
                Label="security-floor: #:package System.Text.RegularExpressions@4.3.1;
                       until: transitive-floor(Serilog) >= 4.3.1" />
```

Now the entry explains itself, and states the condition under which it can go.

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
| `RDK0004` | warning | A declared version that no project references, carrying no hint |
| `RDK0005` | error | A .NET tool package that cannot be installed or run |
| `RDK0006` | warning | A build file nothing inside the package imports |
| `RDK0007` | warning | Output copies MSBuild does not track in `FileWrites` |
| `RDK0008` | warning | Analyzer assemblies under a target framework folder |
| `RDK0009` | warning | Symbol package that does not cover every shipped assembly |
| `RDK0010` | warning | Assembly under a `lib/<framework>/` folder it does not satisfy |

### Shipping a package? Check it before it is permanent

`RDK0001`, `RDK0005` and `RDK0006` describe problems that survive build, pack, restore **and**
publish — discovered only by whoever installs what you shipped, on a version nuget.org will let you
unlist but never delete.

```console
dotnet pack -c Release
redecker inspect --file ./artifacts/packages/*.nupkg
```

`RDK0005` came out of searching GitHub for real reports: `DotnetToolSettings.xml` missing from a
published tool package turns up in repository after repository, dotnet/runtime included.

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
- **A shared hint registry** — the largest idea here, and the least worked out. Hints are already
  a machine-readable format for *why a dependency looks the way it does*; today they live in one
  repository's `Directory.Packages.props`. Published and queryable, they become the data layer that
  the new generation of AI upgrade agents has to guess at — and the one plausible route to making
  [epochs](#not-supported-epochs) tractable, by describing a migration rather than trying to encode
  it in a version ordering. Needs a trust model before it needs code.

## Alternatives

Redecker is not the first tool to notice that .NET dependency manifests drift from reality. Two
existing pieces of work cover ground next to it, both worth using, and one of them removes part of
Redecker's job outright.

| | Question it answers | Needs |
| --- | --- | --- |
| [Package pruning](https://devblogs.microsoft.com/dotnet/nuget-package-pruning-in-dotnet-10/) (.NET 10 SDK) | Is this package already supplied by the platform? | Restore, `net10.0`+ |
| [Snitch](https://github.com/spectresystems/snitch) | Do I reference something directly that another project already brings in? | The resolved graph |
| **Redecker** | Is this package's *content* sound, and does the manifest record *why* it looks like this? | Package bytes, and the declared text |

### NuGet package pruning

On by default for `net10.0` and later, it removes platform-supplied packages from the dependency
graph during restore — reportedly around 70% fewer transitive vulnerability reports and up to half
the restore time. It happens inside restore, which is strictly better than anything a third-party
tool can manage. **Where it overlaps with Redecker, use pruning.**

Two boundaries matter:

- **It prunes what the platform supplies, at the versions it supplies.** `net8.0` ships
  `System.Text.Json` 8.0.x, so a transitive dependency in that range disappears; a dependency on
  **9.0.0** does not, because the platform does not supply it. That remaining case is exactly the
  [framework band](#the-framework-band-problem) problem. Pruning removes the easy half and leaves
  the half that was already interesting.
- **Direct references are marked, not removed.** Pruning sets `PrivateAssets="all"` rather than
  deleting the line, so the manifest keeps naming a package that no longer participates as it
  appears to — the same class of problem as [RDK0004](#rdk0004-undocumented-transitive-pins),
  arriving from the other direction.

### Snitch

Patrick Svensson's [Snitch](https://github.com/spectresystems/snitch) finds direct references you
can delete because a referenced project already supplies them, and flags shared dependencies that
have been quietly upgraded or downgraded between projects. It and RDK0004 are near mirror images:

- **Snitch:** *you reference this directly, and you did not need to.*
- **RDK0004:** *you declared a version for this, and nothing references it.*

Snitch reasons over the resolved graph, which is what lets it name *who* supplies the package.
Redecker's check is a text comparison needing no restore. Neither subsumes the other.

### The gap between them

Both Snitch and pruning answer **can this be removed?** Neither can answer **should it be?**,
because neither has any record of why the entry exists.

A `PackageVersion` floated above a vulnerable version looks exactly like redundancy. A tool that
reports it as removable is, in that case, recommending you reintroduce a CVE — not a criticism of
Snitch, since the information simply is not in the file to read.

That is the argument for [pin hints](#pin-hints). Removal advice is only safe once intent sits
next to the version, with a condition saying when the reason expires.

## Not supported: epochs

Redecker has nothing useful to say about `xunit` → `xunit.v3`, and neither does any other .NET
dependency tool. This is a limit of the ecosystem rather than a gap in the roadmap, so it is worth
being explicit about.

When a project is redesigned thoroughly enough that continuing the version line would mislead
people, the .NET convention is to publish under a **new package id**. xUnit v3 is a redesign of the
execution model — test assemblies are now executables — so it ships as `xunit.v3` rather than
`xunit` 3.0.0.

That leaves NuGet describing the situation with the only vocabulary it has:

```
xunit 2.9.3    deprecated, reason: Legacy
               "This package will only be updated for security issues.
                All future feature work has moved onto v3."
               alternate package: xunit.v3, version range: *
```

Two things are wrong with that, and neither is xUnit's fault:

- **"Deprecated" conflates *abandoned* with *done*.** The message says the package still receives
  security fixes. Brad Wilson's framing — that v2 is finished rather than deprecated — is the
  accurate one, and NuGet has no way to express it.
- **The alternate range is `*`.** There is no way to say "2.9.3 corresponds to 3.x". The
  correspondence between the old line and the new one is simply not representable.

NuGet's `alternatePackage` is a real escape hatch — the closest thing it has to an epoch, recording
that the line continues under a different identity. What it cannot carry is the version
correspondence, or whether the move is a rename or a rewrite of your test host. That second part is
the only bit you actually needed.

SemVer has no concept of an epoch, and no mainstream package manager for .NET, npm or NuGet
supports one. Two ecosystems do:

| Ecosystem | Syntax | Example |
| --- | --- | --- |
| Python ([PEP 440](https://peps.python.org/pep-0440/#version-epochs)) | `N!` | `1!1.0` |
| Debian | `N:` | `1:1.0` |

Both exist for exactly this: resetting version ordering when a project's versioning scheme changes
drastically, so `1!1.0` sorts above `2.0` from the previous era. Anthony Fu's
[Epoch Semantic Versioning](https://antfu.me/posts/epoch-semver) proposes getting the same effect
without changing any tooling, by folding the epoch into the major component
(`EPOCH * 1000 + MAJOR`).

**What this means here.** Every Redecker rule operates on *the same package at two versions* —
comparing assets, checking families, re-evaluating pins. An epoch change is a change of package
*identity*, so there is no "from" and "to" for the rules to compare. Detecting that a deprecated
package names an alternate is easy and nearly useless: it cannot tell you whether the migration is
a version bump or a rewrite of your test host. Until that distinction is expressible, pretending
to handle it would be worse than declining to.

## Building

```console
dotnet build Redecker.slnx -c Release
dotnet test  Redecker.slnx -c Release --filter "TestCategory!=Network"   # offline
dotnet test  Redecker.slnx -c Release --filter "TestCategory=Network"    # nuget.org + GitHub API
```

The documentation site is VitePress:

```console
cd docs-website
npm ci
npm run docs:dev      # or docs:build
```

`ignoreDeadLinks` is off, so a broken internal link fails the build rather than shipping.

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

2. **Add `NUGET_USER`** to the `release` environment (Settings → Environments → release), as
   either a secret or a variable — the workflow accepts both. Set it to the nuget.org
   **profile name of whoever creates the policy**.

   > This is the single easiest field to get wrong, in two different ways.
   >
   > It is *not* the policy name, and *not* `fluentfoundation` — an organization can own a
   > policy, but the token exchange authenticates the individual who created it.
   > `NuGet/login` sends this value as `username` and reports `Make sure you are using the
   > username of the policy creator, not the policy owner` when it is wrong.
   >
   > And secrets and variables sit side by side in the same settings page, so adding it as one
   > and reading the other yields an empty string rather than an error. The workflow reads
   > `secrets.NUGET_USER || vars.NUGET_USER` for that reason, and fails with a named error if
   > neither is set.

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
