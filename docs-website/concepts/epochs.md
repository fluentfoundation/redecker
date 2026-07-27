# Epochs — what Redecker does not handle

Redecker has nothing useful to say about `xunit` → `xunit.v3`, and neither does any other .NET
dependency tool. This is a limit of the ecosystem rather than a gap in the roadmap, which is why
it gets a page rather than a roadmap bullet.

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

Two things are wrong there, and neither is xUnit's fault.

### "Deprecated" conflates *abandoned* with *done*

The deprecation message says the package still receives security fixes. That is not an abandoned
package — it is a finished one. Brad Wilson's framing, that v2 should be thought of as **done**
rather than deprecated, is the accurate description, and NuGet has no way to record it.

The difference matters to anyone reading the flag. "Abandoned" means *migrate before this becomes
a liability*. "Done" means *this is complete; migrate when the new design is worth it to you*.
Those imply very different urgency, and tooling that acts on the deprecation flag cannot tell
them apart.

### The alternate version range is `*`

There is no way to express that `2.9.3` corresponds to `3.x`. The relationship between the old
line and the new one is simply not representable, so an automated migration has nothing to
compute against.

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

## Why Redecker declines

Every rule here operates on **the same package at two versions**: comparing shipped assets,
checking that a family agrees with itself, re-evaluating whether a pin still applies. An epoch
change is a change of package *identity*, so there is no "from" and "to" for any of that to
compare.

Detecting that a deprecated package names an alternate is easy, and nearly useless. It cannot
tell you whether the migration in front of you is a version bump or a rewrite of your test host —
and that is the only part you actually needed to know.

Until the distinction is expressible, claiming to handle it would be worse than declining to.

## If you are doing an epoch migration

Treat it as a project, not a dependency update. The one thing worth carrying across is the
reasoning: a [pin hint](/concepts/pin-hints) with `until: review` records that you are on the old
line deliberately, and that no tool should propose the move on your behalf.

```xml
<PackageVersion Include="xunit" Version="2.9.3"
                Label="api-compat: #:package xunit@2.9.3; until: review;
                       note: v3 is a new execution model, not a version bump - migrate deliberately" />
```
