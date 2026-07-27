# Alternatives and neighbours

Redecker is not the first tool to notice that .NET dependency manifests drift from reality. Two
existing pieces of work cover ground next to it, and both are worth using. This page is about
where the boundaries actually fall, including where they leave Redecker with less to do.

| | Question it answers | Needs |
| --- | --- | --- |
| [Package pruning](https://devblogs.microsoft.com/dotnet/nuget-package-pruning-in-dotnet-10/) (.NET 10 SDK) | Is this package already supplied by the platform? | Restore, `net10.0`+ |
| [Snitch](https://github.com/spectresystems/snitch) | Do I reference something directly that another project already brings in? | The resolved graph |
| **Redecker** | Is this package's *content* sound, and does the manifest record *why* it looks like this? | Package bytes, and the declared text |

## NuGet package pruning

Shipped in the .NET 10 SDK and **on by default** for `net10.0` and later, controlled by
`RestoreEnablePackagePruning`. It removes platform-supplied packages from the dependency graph at
restore time, which Microsoft reports cuts transitive vulnerability reports by around 70% and
restore time by up to half.

This is a genuinely better fix than anything a third-party tool can do, because it happens inside
restore. Where it overlaps with Redecker, **use pruning.**

Two boundaries are worth knowing precisely.

**It prunes what the platform supplies, at the versions it supplies.** The SDK knows that `net8.0`
ships `System.Text.Json` 8.0.x, so a transitive dependency in that range disappears. A transitive
dependency on `System.Text.Json` **9.0.0** does not, because the platform does not supply that
version.

That second case is exactly the [framework band](/concepts/framework-bands) problem — a package
dragged past the generation its target framework provides. So pruning does not compete with band
analysis; it removes the easy half and leaves precisely the half that was already the interesting
one.

**Direct references are marked, not removed.** Pruning applies `PrivateAssets="all"` to a direct
reference rather than deleting it, so the line stays in your project until a human removes it.
The manifest continues to name a package that no longer participates the way it appears to — which
is the same class of problem [RDK0004](/rules/rdk0004) exists for, arriving from a different
direction.

## Snitch

Patrick Svensson's [Snitch](https://github.com/spectresystems/snitch) finds direct package
references you can delete because a referenced project already brings them in transitively. It
also flags where a shared dependency has been silently upgraded or downgraded between projects.

Snitch and [RDK0004](/rules/rdk0004) are close to mirror images:

- **Snitch:** "you reference this directly, and you did not need to."
- **RDK0004:** "you declared a version for this, and nothing references it."

Snitch reasons over the resolved graph across a solution, which is what lets it say *who* supplies
the package. Redecker's check is a text comparison over declared items and needs no restore at
all. Neither subsumes the other, and running both is reasonable.

### The gap between them is the interesting part

Both Snitch and pruning answer *can this be removed?* Neither can answer *should it be?*, because
neither has any record of why the entry exists.

A `PackageVersion` floated above a vulnerable version looks exactly like redundancy. A tool that
confidently reports it as removable is, in that specific case, recommending you reintroduce a CVE.
That is not a criticism of Snitch — the information simply is not in the file for it to read.

Which is the whole argument for [pin hints](/concepts/pin-hints). Removal advice is only safe when
intent is recorded alongside the version, and an exit condition says when the reason expires:

```xml
<PackageVersion Include="System.Text.RegularExpressions" Version="4.3.1"
                Label="security-floor: #:package System.Text.RegularExpressions@4.3.1;
                       until: transitive-floor(Serilog) >= 4.3.1" />
```

With that present, RDK0004 goes quiet, a human reading the diff knows why the line is there, and
the condition can be re-checked on every run rather than remembered.

## What none of them do

All three read metadata, graphs and manifests. Only Redecker opens the package and compares what
one version ships against another — which is where [RDK0001](/rules/rdk0001) and
[RDK0002](/rules/rdk0002) live, and why a restore-clean upgrade can still fail at build time on a
single target framework.

And none of them, Redecker included, can help with [epoch changes](/concepts/epochs).
