# Real-world examples

Every rule here came from a package that actually shipped, not from imagination. This page is the
evidence log: what was found, in what, and what it cost. New rules get added to it as they are
written, and rules that cannot point at a real case do not get written.

It doubles as a regression suite. Most of these run as network tests on every pull request, so a
rule that stops detecting its own motivating case fails the build.

## SQLitePCLRaw ships a dangling native asset

**Package:** `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 · **Rules:** [RDK0001](/rules/rdk0001),
[RDK0002](/rules/rdk0002)

2.1.12 stopped shipping `runtimes/win-arm/native/e_sqlite3.dll`, but kept the
`buildTransitive/net461` targets file that copies it.

```
error MSB3030: Could not copy the file ".../runtimes/win-arm/native/e_sqlite3.dll"
because it was not found.
```

Restore, resolution and every target framework except `net48` on Windows were green. The upgrade
was taken to clear a security advisory, so it looked mandatory.

**Cost:** one CI round, one red pull request, one revert commit.

**Also found:** the same upgrade dropped **five** runtime identifiers — `win-arm`, `win10-arm`,
`win10-arm64`, `win10-x64`, `win10-x86` — not just the one that broke the build. Nobody had
noticed the other four.

## Microsoft.Data.SqlClient.SNI copies without telling MSBuild

**Package:** `Microsoft.Data.SqlClient.SNI` 6.0.2 · **Rule:** [RDK0007](/rules/rdk0007)

Its `net462` targets copy native SNI binaries into `$(OutDir)` and never record the result in
`@(FileWrites)`:

```xml
<Target Name="CopySNIFiles" ...>
  <Copy SourceFiles="@(SNIFiles)"
        DestinationFiles="@(SNIFiles -> '$(OutDir)%(RecursiveDir)%(Filename)%(Extension)')" />
</Target>
```

MSBuild therefore has no record that those files exist, which is why the same package has to ship
a hand-rolled `CleanSNIFiles` target that `Delete`s them. That target is the tell: you only need
your own `Clean` when the framework was never told what you wrote.

This is a .NET Framework problem specifically. `net4x` predates the runtime asset resolution that
makes native-asset copying automatic on .NET Core, so packages shipping native binaries to `net48`
hand-roll the copy — and the accounting is the easy part to leave out.

## The DotnetToolSettings.xml cluster

**Rule:** [RDK0005](/rules/rdk0005)

Searching GitHub for `DotnetToolSettings.xml` surfaces the same failure across unrelated
repositories, including `dotnet/runtime` and `unoplatform/uno`:

```
Settings file 'DotnetToolSettings.xml' was not found in the package.
```

A tool package missing it builds, packs, restores and **publishes** — then fails for every user
who tries to install, on a version nuget.org will let you unlist but never delete.

::: warning An honest caveat
These reports do **not** all share one root cause. Some are genuine packaging defects; others
turned out to be SDK-side problems producing the same message. `Spriggit.CLI` 0.41.0, named in one
report, ships its settings file correctly. RDK0005 catches the kind you can do something about,
and the rule is tested synthetically because no live broken package could be found.
:::

## EF Core, a family split by an updater

**Rule:** [RDK0003](/rules/rdk0003)

EF Core [requires](https://learn.microsoft.com/en-us/ef/core/what-is-new/nuget-packages#package-versions)
that every `Microsoft.EntityFrameworkCore.*` package carry the same version. Per-package updaters
see packages rather than families, so they bump whichever members happen to have newer releases
and open a pull request that restores cleanly.

The failure lands at run time, as a missing type or a provider that does not match its core
package.

## What the false-positive sweeps caught

Running the rules against real packages has repeatedly been more useful than writing more tests.

**RDK0001 accused Microsoft.Data.SqlClient.SNI of a defect it does not have.** The package imports
a `.targets.user` file if the consumer has written one:

```xml
<Import Condition="... Exists('$(MSBuildThisFileDirectory)....targets.user')"
        Project="$(MSBuildThisFileDirectory)....targets.user" />
```

That file is *meant* to be absent — it is an extension point. The rule now understands
`Exists(...)` guards, and a test pins the distinction: an unrelated `Exists()` elsewhere in a
condition must not excuse every other reference in scope.

The lesson generalises. Every rule here is checked against a spread of real packages before being
believed, because a rule that fires on healthy packages is worse than no rule at all.

## The corpus sweep

Hand-picking examples does not scale, and it biases the rule set towards whatever broke recently
in one person's build. `tools/Redecker.Corpus` runs every single-package rule across the
most-downloaded packages on nuget.org. Downloads are cached, so a repeat sweep of 500 packages
takes about three seconds.

### Results across the top 500

| Rule | Packages | Rate |
| --- | ---: | ---: |
| RDK0001 | 0 | 0% |
| RDK0005 | 0 | 0% |
| RDK0006 | 1 | 0.2% |
| RDK0007 | 2 | 0.4% |

That is the state *after* the sweep found and killed three false positives. Before it, the same
run reported findings against Grpc.Tools, coverlet.collector and
Microsoft.AspNetCore.Components.Analyzers — none of which have the defect claimed.

### Three bugs it caught

**Grpc.Tools: RDK0006 misunderstood build folders.** `build/Grpc.Tools.props` exists and imports
`_grpc/_Grpc.Tools.props` and `_protobuf/Google.Protobuf.Tools.props`. Those subfolders are
helpers reached from a correctly named entry point, which is entirely legitimate. The rule was
treating *every* directory under `build/` as an import root, when only `build/` itself and
`build/<tfm>/` ever are.

**coverlet.collector: RDK0007 was too broad.** It copies only to `$(PublishDir)`, and
`IncrementalClean` governs the build output directory, not publish. `$(PublishDir)` was dropped
from the rule.

**Microsoft.AspNetCore.Components.Analyzers: RDK0001 mis-parsed a path.** The package writes:

```xml
$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)../../analyzers/dotnet/cs/....dll'))
```

The extractor captured the trailing `'))` along with the path, so a file the package genuinely
ships looked missing. Paths embedded in property functions now terminate at the first character
that cannot appear in a package entry.

### What survived

Both remaining RDK0007 findings copy into `$(OutDir)` with no `FileWrites` accounting, which is
exactly the pattern: `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.NET.Sdk.Functions`.

A 0.4% rate on widely-used packages is the right shape. A rule firing on 20% of the top 500 would
be telling you about itself rather than about the ecosystem, which is why the sweep prints an
interpretation beside the number rather than the number alone.

### Running it

```console
dotnet run --project tools/Redecker.Corpus -c Release -- 500 results
```

Results are written to [`results/`](https://github.com/fluentfoundation/redecker/tree/main/results)
and committed, so a later run is a diff rather than a comparison against memory.

## The Microsoft.* and System.* sweep

A second corpus: every reachable package whose id starts with `Microsoft.` or `System.`, published
within six years. **2,680 packages examined.**

It found what it was pointed at. `Microsoft.Data.SqlClient.SNI` reports RDK0007 in every version
checked — 5.2.0, 6.0.1 and 6.0.2 alike — while `Microsoft.Data.SqlClient` itself,
`Microsoft.Data.SqlClient.SNI.runtime` and legacy `System.Data.SqlClient` are all clean. The defect
sits in one package of four, and it is long-standing rather than a regression.

RDK0007's 20 findings cluster exactly where the rule's reasoning predicts: packages shipping native
assets to .NET Framework, which must hand-roll the copy because `net4x` predates automatic runtime
asset resolution. `Microsoft.CognitiveServices.Speech`, `Microsoft.InformationProtection.*`,
`Microsoft.WindowsAppSDK.*`, and the Application Insights family.

It also cost a rule.

## RDK0006 and the limits of a corpus

The Microsoft sweep flagged `Microsoft.NET.Sdk.Razor`, `Microsoft.DotNet.ILCompiler`,
`Microsoft.Maui.Controls.Build.Tasks`, `Microsoft.NET.ILLink.Tasks` and
`Microsoft.Windows.SDK.BuildTools.MSIX` — all of which work.

Their MSBuild files are imported **from outside the package**: by the .NET SDK, by a workload, by
another package, or by a consumer writing an explicit `<Import>`. None of that is visible from the
package contents. There is no reliable marker separating them either — `Microsoft.DotNet.ILCompiler`
carries no `packageType` and no `Sdk/` folder, yet its `Microsoft.NETCore.Native.targets` is
imported by the SDK's publish pipeline.

The first instinct was to retire the rule. That was wrong, and worth recording why.

**A corpus shows the presence of false positives, not the absence of usefulness.** Twenty findings
across 2,680 packages says the rule is imprecise in one identifiable situation; it does not say the
underlying problem is not real. `Microsoft.Azure.StreamAnalytics.CICD` genuinely ships a
`build/StreamAnalytics.targets` that nothing auto-imports, and that is exactly the defect the rule
exists to find.

What the evidence actually justified was **scoping, not deletion**:

- The severity is a **warning**, because the rule cannot see every consumer and should not pretend
  otherwise.
- The finding text **names the benign explanation**, so a reader knows what to check rather than
  being told they have a bug.
- The documentation is explicit that this rule earns its place on **your own package before you
  publish** — where you know whether the SDK imports your targets by path — and needs the caveat
  when run across other people's.

Losing a real defect to avoid an explainable warning is the worse trade.

## The top 2,000, and why retiring RDK0006 would have been wrong

Widening the sweep from 500 to 2,000 packages put RDK0006 at 15 packages — and every new one was a
false positive, on well-formed third-party packages rather than SDK-shipped ones:
`CommunityToolkit.Mvvm`, `Verify`, `Nuke.Common`, `PostSharp`, `Microsoft.Web.WebView2`.

All of them had correct entry points that **did** reference the flagged files. The walker was not
following them, for two distinct reasons:

**An import whose path lives entirely in a property.** `CommunityToolkit.Mvvm` and `Nuke.Common`
both do this:

```xml
<Import Project="$(_CommunityToolkitMvvmSourceGeneratorsTargets)" />
<Import Project="$(NukeTasksDirectory)\Nuke.MSBuildTasks.targets" />
```

The rule already knew it must stay quiet when it could not resolve an import — but the guard only
fired when the path mentioned `MSBuildThisFileDirectory`, which missed the commonest form entirely.
Any unresolvable import can reach anything, so any unresolvable import now stops the rule.

**A file handed to MSBuild through an extension point rather than an import.** `Verify` assigns its
`.AfterMicrosoftNetSdk.props` to `CustomAfterMicrosoftCommonProps`, so there is no `<Import>` to
follow at all. Naming a file in reachable build logic now counts as reachable.

After both fixes RDK0006 falls from 15 packages to 4 on the top 2,000, the genuine finding against
`Microsoft.Azure.StreamAnalytics.CICD` survives, and what remains is the documented
SDK-consumption limitation.

**This is the argument against retiring a rule because a corpus embarrasses it.** The evidence that
looked like "this rule does not work" was really "this rule has a bug in its import walker". Three
rounds of correction later it is at 0.2%, and the defect it was written to catch is still caught.
Deleting it after round two would have thrown away a working rule and the two false-positive classes
it went on to teach us about.

## What restore actually does about a mismatched provider

[RDK0011](/rules/rdk0011) reports a package left behind by a version bump — a database provider
still on last year's release while EF Core moved a major. The obvious objection is that NuGet
already raises `NU1608` for this, which would make the rule redundant.

Settling that from memory was not good enough, so it was settled with four throwaway projects and a
real `dotnet restore` against real packages:

| What was pinned | Diagnostic | Restore | Build |
| --- | --- | --- | --- |
| `Npgsql` 8.0.11, which declares an unbounded minimum | none at all | succeeds | succeeds |
| `Pomelo` 9.0.0 with EF Core Relational 10.0.0 | **`NU1608` warning** | succeeds | succeeds, **and runs** |
| `Npgsql` 9.0.4 with EF Core Relational 10.0.0 | `NU1608` + `NU1107` error | fails | — |
| `Npgsql` 9.0.4 with EF Core Relational 9.0.0 | `NU1605` error | fails | — |

The second row is the rule's whole justification, and it was not obvious in advance. Pomelo
constrains a single package, so pinning above its range cannot raise the version-conflict error;
the program builds and runs with a provider on a core version it declares it does not support. The
third row differs only because Npgsql constrains *two* packages, which is what turns the warning
into an error — a difference in how the provider was authored, not in how wrong the result is.

The first row is the quiet one. A dependency declared as a bare minimum with no upper bound accepts
anything above it forever, so restore has nothing to say at all.

**The rule that survived this is narrower than the one proposed.** It fires only where restore is
permissive, and stays silent on the two shapes that already fail a build, because a rule that
repeats an error you have already seen is noise.

### And the same evidence caught a bug in the fix

The rule suggests which version to move to. Asked which Pomelo release accepts EF Core 10.0.0, the
first implementation answered **7.0.0** — truthfully. Pomelo 7 declares an unbounded minimum, so it
admits anything above it, exactly like the first row of that table.

Suggesting it would have downgraded a provider by two majors to fix a version bump. Only releases
newer than the one in hand are considered now, and when none of them work the rule says so rather
than inventing something.

## The rule that was almost unusable

[RDK0010](/rules/rdk0010) checks that an assembly under `lib/<framework>/` can actually be loaded
by a project targeting that framework. The obvious implementation compares the folder name against
the assembly's `TargetFrameworkAttribute` and reports any difference. Run over 4,205 packages, that
version produced 285 findings — and most of them were wrong.

A `netstandard2.0` assembly in `lib/net8.0/` is not a mistake; it is the ordinary way to win
nearest-framework matching. A `net45` build in `lib/net452/` is a build reused rather than
repeated. Both differ. Both work.

The fix was to stop asking whether two strings match and start asking the question restore asks:
**can a project targeting this folder consume this assembly?** NuGet already answers that, and
handing the question to `NuGet.Frameworks` deleted the version arithmetic along with the false
positives. 285 findings became 87.

Then half of those 87 turned out to be in dead platforms — PCL profiles, Silverlight, Windows
Phone, Windows Store, MonoAndroid, Xamarin, UAP, Tizen — where `MonoAndroid403` is an OS version
and `uap10.0` means `.NETCore,Version=v5.0`. Those comparisons are not wrong, they are
unactionable: the tooling that would republish those packages no longer exists. Scoping the rule to
`.NETFramework`, `.NETCoreApp` and `.NETStandard` left 22 packages in 4,205.

| Version of the rule | Findings | Useful |
| --- | ---: | --- |
| Compare the strings | 285 | mostly not |
| Ask NuGet about compatibility | 87 | yes, plus dead platforms |
| Scope to living frameworks | 42 | yes |

Two lessons, and the second is the one that keeps paying:

**Where a question has an authoritative implementation, call it.** Framework compatibility is not
derivable from the folder names — netstandard has a fallback chain, PCL profiles are set
intersections. Restore already knows all of it.

**A rule nobody can act on is a rule people suppress**, and a suppressed rule takes the findings
next to it down with it.

## Symbol coverage, and what 232 packages settled

Before writing [RDK0009](/rules/rdk0009), the question was whether partial symbol coverage happens
at all. Of 232 corpus packages shipping `lib/` assemblies:

| Outcome | Packages |
| --- | ---: |
| No symbol package at all | 174 |
| Symbols covering every shipped assembly | 57 |
| Symbols with a gap | 1 |

Two things follow, and both shaped the rule.

**Publishing no symbols is a choice, not a defect** — three quarters of packages make it, so the
rule stays silent there rather than nagging.

**Complete coverage is the convention when symbols do ship**, at 57 of 58. That is what makes a gap
worth reporting: it is not a matter of taste, it is a deviation from what essentially everybody
does.

And the single exception taught the exclusion. `Microsoft.VisualStudio.Validation` has 26 uncovered
assemblies, all satellites — `lib/net8.0/de/…resources.dll` and its siblings — which correctly have
no PDBs, because a resource assembly has no code to step through. Only assemblies directly in
`lib/<framework>/` are considered.

Had the rule been written from the idea alone, that package would have been its first false
positive.

## Rules we decided not to write

Knowing why something was rejected is worth as much as knowing why something shipped.

### Analyzers in the wrong folder

The idea: a Roslyn analyzer only loads from `analyzers/dotnet/<lang>/`, so one packed anywhere
else is silently inert — the same shape as [RDK0006](/rules/rdk0006), and an appealing rule.

The corpus said no. Surveying every `analyzers/` path across the top 500, all of these are in use
and all of them work:

| Layout | Example |
| --- | --- |
| `analyzers/dotnet/cs` | the common case |
| `analyzers/cs`, `analyzers/vb` | `Microsoft.VisualStudio.Threading.Analyzers` |
| `analyzers/dotnet` | `System.Reactive`, language-agnostic |
| `analyzers/dotnet/roslyn4.8/cs` | `Refit`, Roslyn-version-specific |
| `analyzers/dotnet/cs/de`, `/ja`, `/zh-Hans` | `System.Text.Json`, localised resources |
| `analyzers/dotnet/roslyn4.4/cs/pt-BR` | versioned *and* localised |

The proposed check would have accused `System.Text.Json`, `System.Reactive` and `StyleCop.Analyzers`
of a defect none of them has. And **zero** packages in the top 500 had a genuinely broken layout —
the failure being guarded against does not appear in practice.

It could be salvaged by encoding every valid shape plus a locale list, but a rule that is 90%
allowlist restates the convention rather than checking it. Closed as
[#2](https://github.com/fluentfoundation/redecker/issues/2).

The premise came from reasoning by analogy with RDK0006 rather than from anything observed.
Plausible, and wrong — which is the useful kind of wrong to find before writing the code.

It needs no Azure subscription and no credentials. The nuget.org search endpoint ranks all
471,406 packages by download count and pages with skip/take, and the flat container serves every
`.nupkg` over plain HTTP — so a top-N sweep needs no Azure subscription and no credentials.

Tracked in [issue #6](https://github.com/fluentfoundation/redecker/issues/6).
