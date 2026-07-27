# Pin Hints

When you hold a package back, the reason lives in a comment no tool can act on. So the pin
outlives its cause, and eventually nobody dares remove it because nobody remembers what it was
protecting against.

A hint records the reason **and the condition under which it stops applying**.

```xml
<PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.11"
                Label="upstream-bug: #:package SQLitePCLRaw.bundle_e_sqlite3@2.1.11;
                       until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12);
                       note: 2.1.12 stopped shipping the win-arm native asset its targets copies" />
```

## Why `Label`

`Label` is a plain MSBuild attribute that NuGet ignores, so this needs no schema change and no
sidecar file. The subject reuses the `#:package Id@Version` directive syntax from file-based
apps, so "this package at this version" is spelled the same way in both places.

::: warning
`Label` is opaque to the SDK. `dotnet package add` and `dotnet package update` rewrite these item
elements and have no reason to preserve an attribute they do not know about. Worth verifying
against your own workflow before relying on it heavily.
:::

## Grammar

```
<kind>: #:package <Id>[@<Version>][; until: <condition>][; note: <text>]
```

A label on an `<ItemGroup>` applies to every package inside it, which is the natural place for a
hint covering a whole family.

### Kinds

| Kind | Means | Retires when |
| --- | --- | --- |
| `security-floor` | An explicit reference only exists to lift a vulnerable transitive floor | the parent raises its own floor |
| `upstream-bug` | A newer version is broken | upstream fixes it |
| `framework-band` | Tied to a target framework's generation | never — recomputed per framework |
| `api-compat` | Avoiding a breaking change | human review |
| `transitive-conflict` | Settling a version conflict | the conflict resolves |

### Exit conditions

| Condition | Retires when | Evaluated today |
| --- | --- | --- |
| `package-assets-intact(Id@Version)` | that version stops failing the package rules | ✅ |
| `issues-closed(123, 456)` | every issue is closed **as completed** | ✅ |
| `issues-released(123)` | …and the closing commits have reached a release tag | ✅ |
| `transitive-floor(Id) >= 1.2.3` | a dependant raises its own floor | needs a resolved graph |
| `advisory-clear(GHSA-…)` | the advisory stops applying | needs the advisory database |
| `never` / `review` | structural / human decision | — |

## Waiting on upstream issues

```xml
Label="upstream-bug: #:package Some.Package@1.2.3; until: issues-released(1234, 1235)"
```

The repository is **not** named. It comes from the pinned package's own nuspec, so a hint states
only which issues it waits on — and stays correct if the project moves, because the URL is read
from whichever version is pinned.

Two deliberate distinctions:

- **Closed as *not planned* does not discharge a pin.** The tracker is tidy, but upstream has
  declined to fix the defect, so the pin is still earning its place. Only *closed as completed*
  counts.
- **`issues-closed` is not `issues-released`.** A fix merged to `main` is not a fix you can
  consume.

### It does not clone anything

`git tag --contains` needs the commit graph, but the GitHub compare endpoint answers the same
question directly: comparing a tag against a commit returns `identical` or `behind` when the tag
contains it, `ahead` when it does not. Containment is monotonic along an ordered release history,
so the earliest containing tag is found by binary search over version-sorted tags — on a
repository with 72 tags that is **8 requests instead of 73**, and no clone at all.
