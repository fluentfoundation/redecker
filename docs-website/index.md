---
layout: home

hero:
  name: Redecker
  text: Green build. Broken package.
  tagline: restore only checks the maths. It never opens the box. Redecker does — and catches the upgrades that pass every check you run and fail anyway.
  image:
    src: /redecker-icon-512.png
    alt: Redecker
  actions:
    - theme: brand
      text: Show me
      link: /problems
    - theme: alt
      text: Install
      link: /guide/getting-started
    - theme: alt
      text: GitHub
      link: https://github.com/fluentfoundation/redecker

features:
  - title: Builds everywhere except one place
    details: A package points at files it no longer ships. Restore is fine. net48 on Windows is not.
    link: /rules/rdk0001
    linkText: RDK0001 →

  - title: Platforms that vanish quietly
    details: An upgrade stops shipping a runtime identifier. You find out on the device, not in CI.
    link: /rules/rdk0002
    linkText: RDK0002 →

  - title: Your updater split the family
    details: EF Core packages must match each other. Bots bump them one at a time. Restore says fine.
    link: /rules/rdk0003
    linkText: RDK0003 →

  - title: The pin nobody can delete
    details: Someone floated a floor to dodge a CVE. A year on, it just looks like clutter. Delete it and the CVE is back.
    link: /rules/rdk0004
    linkText: RDK0004 →

  - title: Pins with an expiry date
    details: Record why a pin exists and what would end it. The tool re-checks every run and tells you when it can go.
    link: /concepts/pin-hints
    linkText: Pin hints →

  - title: Right package, wrong generation
    details: A 9.0 extension in a net8.0 app works — it just quietly ships unoptimised. Nothing warns you.
    link: /concepts/framework-bands
    linkText: Framework bands →
---

## Thirty seconds

Your dependency tooling asks one question: **does it restore?**

Restore is a constraint solver. It reads version numbers, checks the arithmetic, and reports
success. It never opens a package. It has no opinion on whether the versions you declared make
sense together.

So this passes:

```console
$ dotnet restore
Restored in 3.2s.
```

And this is what was actually in the box:

```console
$ redecker inspect SQLitePCLRaw.lib.e_sqlite3 --from 2.1.11 --to 2.1.12
error RDK0001: buildTransitive/net461/…targets references
    runtimes/win-arm/native/e_sqlite3.dll, which the package does not contain
warning RDK0002: drops 5 runtime identifiers: win-arm, win10-arm, win10-arm64, win10-x64, win10-x86
```

That upgrade cost a CI round, a red pull request and a revert commit. Redecker found it in about
a second, from metadata, before any of that.

```console
dotnet tool install --global dotnet-redecker   # investigate an upgrade
dotnet add package Redecker.MSBuild            # fail the build instead
```

**[Seven ways this happens →](/problems)**

## What it is not

Not an updater. Not a replacement for `dotnet restore`. Not a resolver.

It answers one question those tools structurally cannot: **is this upgrade sound in ways that
restore succeeding does not prove?**

Where .NET 10's package pruning overlaps with it, use pruning — it happens inside restore, which
beats anything bolted on. The [comparison](/comparison) is honest about exactly where that line
falls, and about the one thing no tool here can help with:
[epochs](/concepts/epochs).
