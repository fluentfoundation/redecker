# Comparison with other .NET tools

Redecker is not the first tool to notice that .NET dependency manifests drift from reality.
Several existing tools cover ground next to it, most are worth using, and one of them removes part
of Redecker's job outright.

The useful way to tell them apart is not what they *do* but **what they read**.

| Tool | Reads | Answers |
| --- | --- | --- |
| `dotnet list package --outdated/--deprecated/--vulnerable` | Version metadata | What is behind, deprecated, or flagged? |
| [`dotnet-outdated`](https://github.com/dotnet-outdated/dotnet-outdated) | Version metadata | The same, with bulk updating |
| `dotnet package update` (.NET 10 SDK) | Version metadata | Move these references forward |
| [Dependabot](https://github.com/dependabot/dependabot-core) · [Renovate](https://github.com/renovatebot/renovate) | Version metadata | Which upgrades exist, as pull requests |
| [Package pruning](https://devblogs.microsoft.com/dotnet/nuget-package-pruning-in-dotnet-10/) (.NET 10 SDK) | The resolved graph | Is this already supplied by the platform? |
| [Snitch](https://github.com/spectresystems/snitch) | The resolved graph | Do I reference this directly without needing to? |
| [Package validation / ApiCompat](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview) | API surfaces in your package | Are my assets API-consistent with each other? |
| [.NET upgrade tooling](https://github.com/dotnet/modernize-dotnet) | Your source code | How do I move to a newer framework? |
| **Redecker** | **Package contents, and declared intent** | **Is this upgrade sound, and does the manifest say why it looks like this?** |

Nothing else in that table opens a package to compare what one version ships against another, and
nothing else has any way to record *why* a pin exists.

## Updaters: Dependabot, Renovate, dotnet-outdated

These answer "what is behind?" and, in the first two cases, raise the pull request for you. They
are the tools most repositories already run, and Redecker replaces none of them — it has no
opinion about which version you should move to.

The relationship is that an updater's output is Redecker's input. [RDK0003](/rules/rdk0003) exists
precisely because a per-package updater *causes* split families: it sees packages rather than
families, bumps whichever members happen to have newer releases, and produces a pull request that
restores cleanly and fails at run time.

::: tip
[NuKeeper](https://github.com/NuKeeperDotNet/NuKeeper) still appears in a lot of older advice. It
was archived in 2022 and should not be adopted for new work.
:::

## NuGet package pruning

Shipped in the .NET 10 SDK and **on by default** for `net10.0` and later, controlled by
`RestoreEnablePackagePruning`. It removes platform-supplied packages from the dependency graph at
restore time — reportedly around 70% fewer transitive vulnerability reports and up to half the
restore time.

This is a better fix than anything a third-party tool can manage, because it happens inside
restore. Where it overlaps with Redecker, **use pruning.**

Two boundaries are worth knowing precisely.

**It prunes what the platform supplies, at the versions it supplies.** The SDK knows `net8.0`
ships `System.Text.Json` 8.0.x, so a transitive dependency in that range disappears. A dependency
on **9.0.0** does not, because the platform does not supply that version.

That remaining case is exactly the [framework band](/concepts/framework-bands) problem — a package
dragged past the generation its framework provides. Pruning removes the easy half and leaves
precisely the half that was already interesting.

**Direct references are marked, not removed.** Pruning applies `PrivateAssets="all"` rather than
deleting the line, so the manifest keeps naming a package that no longer participates as it
appears to — the same class of problem [RDK0004](/rules/rdk0004) exists for, arriving from the
other direction.

## Snitch

Patrick Svensson's [Snitch](https://github.com/spectresystems/snitch) finds direct package
references you can delete because a referenced project already supplies them transitively, and
flags shared dependencies quietly upgraded or downgraded between projects.

Snitch and [RDK0004](/rules/rdk0004) are close to mirror images:

- **Snitch:** "you reference this directly, and you did not need to."
- **RDK0004:** "you declared a version for this, and nothing references it."

Snitch reasons over the resolved graph across a solution, which is what lets it name *who* supplies
a package. Redecker's check is a text comparison over declared items and needs no restore. Neither
subsumes the other, and running both is reasonable.

### The gap between them is the interesting part

Both Snitch and pruning answer *can this be removed?* Neither can answer *should it be?*, because
neither has any record of why the entry exists.

A `PackageVersion` floated above a vulnerable version looks exactly like redundancy. A tool that
confidently reports it as removable is, in that specific case, recommending you reintroduce a CVE.
That is not a criticism of Snitch — the information is not in the file for it to read.

Which is the entire argument for [pin hints](/concepts/pin-hints). Removal advice is only safe when
intent sits beside the version, and an exit condition says when the reason expires.

## Package validation and ApiCompat

The SDK's `EnablePackageValidation`, and the
[`Microsoft.DotNet.ApiCompat.Tool`](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview)
behind it, validate that a multi-targeting package is **API-consistent**:

| Validator | Checks |
| --- | --- |
| Baseline version | No breaking changes against a previously released version |
| Compatible runtime | Runtime-specific assets match the compile-time ones |
| Compatible framework | Code compiled against one framework runs against the others |

If you ship a library, turn it on. It answers a question Redecker does not ask, and answers it
better than any third-party tool could — it is comparing API surfaces, with Microsoft's own
compatibility rules.

The two are orthogonal. Package validation asks whether your package's **APIs** are consistent
across the assets it ships. Redecker asks whether the package is **structurally** capable of doing
its job at all: whether its MSBuild logic points at files that exist
([RDK0001](/rules/rdk0001)), and whether a tool package can actually be installed
([RDK0005](/rules/rdk0005)), and whether its build logic is
reachable at all ([RDK0006](/rules/rdk0006)).

A package can pass every API validator and still be uninstallable.

## .NET upgrade tooling

The .NET Upgrade Assistant lineage — now [modernize-dotnet](https://github.com/dotnet/modernize-dotnet),
whose README in turn points at a successor called `upgrade-agent` — moves projects to newer target
frameworks and off .NET Framework. It reads your **source code**, which nothing else here does,
and the current generation is an **AI agent** requiring a GitHub Copilot subscription.

That direction of travel matters to Redecker, and not as competition.

An agent asked to move a codebase forward needs to know things that exist in nobody's metadata:
that this pin is floated above an advisory and must not be dropped; that this family moves in
lockstep; that `xunit` → `xunit.v3` is a redesign of the test host rather than a version bump.
Today it has to infer that from prose, or guess.

Hints are that knowledge in machine-readable form. At present they live in one repository's
`Directory.Packages.props`. Published to a shared registry they become the data layer such an
agent is missing — see [epochs](/concepts/epochs) for where that would help most.

## What none of them do

Everything above reads version metadata, dependency graphs, or source code. Only Redecker opens
the package and compares what one version ships against another — which is where
[RDK0001](/rules/rdk0001) and [RDK0002](/rules/rdk0002) live, and why a restore-clean upgrade can
still fail at build time on a single target framework, on a single operating system.

## Honest summary

| If you want | Use |
| --- | --- |
| To know what is out of date | `dotnet list package --outdated`, or an updater |
| Upgrade pull requests raised for you | Dependabot or Renovate |
| A smaller, cleaner restore graph | .NET 10 package pruning — nothing beats it |
| To delete references you do not need | Snitch |
| To move to a newer target framework | The .NET upgrade tooling |
| Your library's APIs to stay consistent | `EnablePackageValidation` — turn it on |
| To know whether an upgrade is *sound* | Redecker |
| Your package to be installable at all | Redecker |
| Your manifest to explain itself | Redecker |
