# Sweep results

Output from `tools/Redecker.Corpus`, committed so that later runs diff against it.

| File | What it is |
| --- | --- |
| `top-<N>.json` | Every package examined, its exact version, and every finding |
| `top-<N>.md` | Human summary: rates per rule, and each finding |

## Why these are committed

A rate in a terminal is a fact about one afternoon. A committed baseline is a fact you can diff.

The question worth answering is not "does RDK0007 fire on 0.4% of packages" but "**did that change,
and which packages moved**". That only works if the previous answer is written down, in a form
where a diff is readable.

## Regenerating

```console
dotnet run --project tools/Redecker.Corpus -c Release -- 500 results
```

Downloads are cached in `~/.redecker-corpus`, so a repeat run takes seconds rather than minutes.
Package versions are immutable, so a cache hit is always valid.

## Two deliberate properties

**The JSON has no timestamp.** Identical results produce a byte-identical file, so `git diff` is
empty unless a rule's behaviour actually changed. Run metadata that changes every time lives in the
Markdown instead, where it is provenance rather than noise.

**Clean packages are recorded, not just findings.** A findings-only file cannot distinguish
"checked and fine" from "never checked", which makes it useless for exactly the comparison these
exist for. 498 of the 500 entries in the current baseline are empty, and that is the point.

## Reading a diff

- A package gaining a finding: either it published something new, or a rule changed. Check which.
- A package losing one: usually a rule was narrowed. That should be deliberate — every narrowing
  so far came with a test pinning it.
- The version changing with no finding change: routine, the corpus moves as packages release.

Three false positives were found and fixed this way on the first run — Grpc.Tools,
coverlet.collector and Microsoft.AspNetCore.Components.Analyzers. See
[evidence](../docs-website/evidence.md).
