# The problems

Every dependency tool in .NET answers one question: **does it restore?**

Restore is a constraint solver over declared version ranges. It is very good at that, and that is
all it does. It never opens a package, never compares one version's contents to another's, and has
no opinion about whether the set of versions you declared makes sense together.

All six of the following restore perfectly.

## 1. A package points at files it does not ship

A package can carry MSBuild logic — `build/`, `buildTransitive/` — that every consuming project
imports. When that logic references a file the package no longer contains, restore and resolution
both succeed, and the build fails on whichever target framework selects that file.

**You find out:** at build time, on one target framework, often on one operating system.

**Real case:** `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 stopped shipping
`runtimes/win-arm/native/e_sqlite3.dll` while its `net461` targets still copied it. Everything
except `net48` on Windows was green. → [RDK0001](/rules/rdk0001)

## 2. An upgrade drops a platform

Packages retire dead platforms, which is legitimate. But a dropped `lib/` framework means
compilation silently binds a different asset, and a dropped runtime identifier means a failure on
that device rather than in CI.

**You find out:** when someone runs it on the platform you stopped shipping for.

**Real case:** the same SQLitePCLRaw upgrade dropped five runtime identifiers, not just the one
that broke the build. → [RDK0002](/rules/rdk0002)

## 3. A package family gets split

Some packages must all carry the same version as each other. EF Core
[says so explicitly](https://learn.microsoft.com/en-us/ef/core/what-is-new/nuget-packages#package-versions).

This is not a mistake people make by hand. It is what an automated updater produces, because it
sees packages rather than families: it bumps whichever members happen to have newer releases,
splits the set, and opens a pull request that restores cleanly.

**You find out:** at run time, as a missing type or a provider that does not match its core
package. → [RDK0003](/rules/rdk0003)

## 4. A package is dragged past its runtime generation

Pull a 9.0 extension into a `net8.0` app and it works. It also lifts those assets out of the
optimised shared framework and ships them app-local. For a library, it quietly raises the floor
every one of your consumers must meet.

**You find out:** never, loudly. Sometimes as a serialization contract that differs, or a type
that is missing on one framework.

The trap here is over-correcting: most of `Microsoft.Extensions.*` is compile-at-head and should
simply take the newest release. Treating a whole prefix as runtime-bound is as wrong as treating
none of it that way. → [Framework bands](/concepts/framework-bands)

## 5. A pin outlives its reason

You hold a package back for a good reason. The reason goes in a comment. Eighteen months later
nobody remembers whether it still applies, so the pin stays forever — and the upgrade it was
deferring never happens.

The fix is not better comments. It is recording **what would have to become true** for the pin to
go away, in a form a tool can re-check on every run.

**You find out:** never. That is the problem. → [Pin hints](/concepts/pin-hints)

## 6. An advisory has no clean upgrade

Sometimes the vulnerable package is transitive and its parent cannot consume the fixed version.
Sometimes the advisory lists no patched version at all. Sometimes the only remedy is a major
version of an entirely different package.

The honest response is a documented hold with an expiry condition attached — not silence, and not
a suppression nobody will revisit. → [Pin hints](/concepts/pin-hints)

## What they have in common

None of them are visible in the dependency graph, because none of them are *about* the dependency
graph. They are about what is inside packages, and about whether the set of versions a repository
declares is coherent.

Both of those can be checked. That is the whole of what Redecker does.

## What it does not do

It will not replace `dotnet restore` or your existing updater, and it has nothing useful to say
about [epoch changes](/concepts/epochs) such as `xunit` → `xunit.v3` — a limit of the ecosystem
rather than a gap in the roadmap.
