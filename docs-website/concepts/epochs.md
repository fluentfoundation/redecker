# Epochs — the case nothing handles well

Redecker has nothing useful to say about `xunit` → `xunit.v3` today, and neither does any other
.NET dependency tool. This is mostly a limit of what the ecosystem can express, which is why it
gets a page rather than a roadmap bullet — and why the [ending](#where-this-could-go-a-hint-registry)
is about changing what can be expressed rather than writing another rule.

## The situation

Sometimes a project is redesigned thoroughly enough that continuing the version line would
mislead people. xUnit v3 is a redesign of the execution model — test assemblies are now
executables — so it ships as a **new package id**, `xunit.v3`, rather than `xunit` 3.0.0.

That is the .NET convention, and it leaves NuGet describing the situation with the only vocabulary
it has:

```
xunit 2.9.3    deprecated, reason: Legacy
               "This package will only be updated for security issues.
                All future feature work has moved onto v3."
               alternate package: xunit.v3, version range: *
```

That is not nothing. **`alternatePackage` is a real escape hatch**, and it is the closest thing
NuGet has to an epoch: it records that the line continues elsewhere, under a different identity.
Tooling can follow it, and this page originally undersold it.

What it cannot carry is everything that makes the migration a decision rather than a lookup.

### "Deprecated" conflates *abandoned* with *done*

The deprecation message says the package still receives security fixes. That is not an abandoned
package — it is a finished one. Brad Wilson's framing, that v2 should be thought of as **done**
rather than deprecated, is the accurate description, and NuGet has no way to record it.

The difference matters to anyone reading the flag. "Abandoned" means *migrate before this becomes
a liability*. "Done" means *this is complete; migrate when the new design is worth it to you*.
Those imply very different urgency, and tooling that acts on the deprecation flag cannot tell
them apart.

### The alternate version range is `*`

The *identity* mapping survives; the *version* mapping does not. `*` says "some version of
`xunit.v3`", not that `2.9.3` corresponds to `3.x`, so ordering across the boundary is undefined
and an automated migration has no correspondence to compute against.

### Nothing says what the migration costs

This is the part no field can express. `alternatePackage` looks identical whether the move is a
package rename with no code change, or a redesign that makes your test assemblies executables and
changes how they are launched.

That distinction is the only thing you actually needed to know, and it is precisely what is
missing.

## Nobody supports epochs

SemVer has no concept of an epoch. Neither NuGet nor npm has one. Two ecosystems do:

| Ecosystem | Syntax | Example |
| --- | --- | --- |
| Python ([PEP 440](https://peps.python.org/pep-0440/#version-epochs)) | `N!` | `1!1.0` |
| Debian | `N:` | `1:1.0` |

Both exist for precisely this case: **resetting version ordering when a project's versioning
scheme changes drastically**, so that `1!1.0` sorts above `2.0` from the previous era without
anyone having to invent a fake `3.0.0`.

Anthony Fu's [Epoch Semantic Versioning](https://antfu.me/posts/epoch-semver) proposes getting the
same effect in ecosystems that have no epoch field, by folding it into the major component —
`EPOCH * 1000 + MAJOR` — so existing tools keep working unchanged and only humans need to learn
the convention. The idea is borrowed from Debian's scheme.

## Why Redecker declines, for now

Every rule here operates on **the same package at two versions**: comparing shipped assets,
checking that a family agrees with itself, re-evaluating whether a pin still applies. An epoch
change is a change of package *identity*, so there is no "from" and "to" for any of that to
compare.

Following `alternatePackage` is easy. Doing something *useful* with it is not, because the field
cannot say whether the move is a rename or a rewrite. Shipping a check that says "an alternative
exists" would add noise without adding judgement.

## Where this could go: a hint registry

The missing information is not missing because it is unknowable. Someone knows that
`xunit` → `xunit.v3` changes the execution model; it is written down in a migration guide, in
prose, for humans.

[Hints](/concepts/pin-hints) are already the format for that kind of knowledge — a reason, a
subject, and a condition — and today they live in a single repository's
`Directory.Packages.props`. There is no reason they have to.

A **shared registry of package hints** would let one person's finding serve everyone:

- *`xunit` → `xunit.v3` is an epoch move; the execution model changed; budget real time*
- *`Some.Package` 4.x cannot consume the fixed `log4net`; the remedy is the 5.x major*
- *`SQLitePCLRaw.lib.e_sqlite3` 2.1.12 has a dangling `win-arm` asset; skip it*

That last one is a fact this tool already computes. Publishing it would mean the next person never
spends a CI round rediscovering it.

The audience for that registry is increasingly not a human. The
[.NET upgrade tooling](/comparison#net-upgrade-tooling) is now an AI agent, and an agent asked to
move a codebase forward has to infer exactly this kind of knowledge from prose, or guess at it.
A queryable, attributable, machine-readable source of *why a dependency looks the way it does*
is the data layer that is missing — and it would make epochs tractable in the one way the version
number never will: by describing the migration rather than trying to encode it in an ordering.

That is a proposal, not a plan. It needs a trust model — signed by whom, trusted on whose word —
before it is anything more.

## If you are doing an epoch migration today

Treat it as a project, not a dependency update. The one thing worth carrying across is the
reasoning: a [pin hint](/concepts/pin-hints) with `until: review` records that you are on the old
line deliberately, and that no tool should propose the move on your behalf.

```xml
<PackageVersion Include="xunit" Version="2.9.3"
                Label="api-compat: #:package xunit@2.9.3; until: review;
                       note: v3 is a new execution model, not a version bump - migrate deliberately" />
```
