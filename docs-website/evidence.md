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

## Corpus scanning

The obvious next step is to stop hand-picking examples. The nuget.org search endpoint ranks all
471,406 packages by download count and pages with skip/take, and the flat container serves every
`.nupkg` over plain HTTP — so a top-N sweep needs no Azure subscription and no credentials.

Tracked in [issue #6](https://github.com/fluentfoundation/redecker/issues/6).
