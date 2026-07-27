---
layout: home

hero:
  name: Redecker
  text: Sweep stale dependencies
  tagline: An update tool for .NET that reads packages, not just their version graph — and knows which dust is load-bearing.
  image:
    src: /redecker-icon-512.png
    alt: Redecker
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: Rules
      link: /rules/
    - theme: alt
      text: GitHub
      link: https://github.com/fluentfoundation/redecker

features:
  - title: Reads the package
    details: >
      A version bump can restore cleanly, resolve cleanly, and still break the build, because a
      package ships MSBuild logic pointing at files it no longer contains. Nothing about that is
      expressed in the dependency graph. Redecker opens the package and looks.
    link: /rules/rdk0001
    linkText: RDK0001

  - title: Pins that retire themselves
    details: >
      Recording why a pin exists is documentation. Recording what would have to become true for
      it to go away lets a tool re-check it every run and tell you when to delete it — so pins
      stop becoming folklore nobody dares remove.
    link: /concepts/pin-hints
    linkText: Pin hints

  - title: Knows the .NET edge cases
    details: >
      Some families are tied to a runtime generation, some must move in lockstep with each other,
      and most of Microsoft.Extensions.* is neither. Treating them all the same is how a generic
      updater produces a broken graph.
    link: /concepts/framework-bands
    linkText: Framework bands

  - title: Fails the build, not a report
    details: >
      The MSBuild package runs the coherence checks during every build, so a split
      Microsoft.EntityFrameworkCore.* family is an error where you will see it, rather than a
      warning in a log nobody reads.
    link: /guide/msbuild
    linkText: MSBuild integration
---

## The case this exists for

`SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 → 2.1.12 clears a security advisory. It restores cleanly,
resolves cleanly, and builds cleanly on every target except `net48` on Windows, where it fails:

```
error MSB3030: Could not copy the file ".../runtimes/win-arm/native/e_sqlite3.dll"
because it was not found.
```

2.1.12 stopped shipping that file but its `buildTransitive/net461` targets still copies it. No
amount of version reasoning finds that. Reading the package finds it in about a second:

```console
$ redecker inspect SQLitePCLRaw.lib.e_sqlite3 --from 2.1.11 --to 2.1.12
error RDK0001: buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets references
    runtimes/win-arm/native/e_sqlite3.dll, which the package does not contain
warning RDK0002: 2.1.11 to 2.1.12 drops 5 runtime identifiers:
    win-arm, win10-arm, win10-arm64, win10-x64, win10-x86
```

Exit code 1 — a CI round, a red pull request, and a revert commit that never had to happen.
