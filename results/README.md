# Sweep results

Output from `tools/Redecker.Corpus`, committed so that later runs diff against it.

| File | What it is |
| --- | --- |
| `top-<N>.json` | Every package examined, its exact version, and every finding |
| `top-<N>.md` | Human summary: rates per rule, and each finding |
| `survey-*.json` / `.md` | A question asked before a rule was written, and what the corpus answered |

## Sweeps and surveys are different things

A **sweep** runs the shipped rules and asks whether any of them has started lying. A **survey** runs
a measurement that is not a rule yet, to decide whether it should become one — issue #4 proposes a
rule and then gates it on evidence, so the survey came first and the rule came second.

Surveys read the download cache rather than the network, which makes them free to re-run. That
matters more than it sounds: the target-framework survey was re-run twice because its first
classifier was wrong, and being able to throw away an answer cheaply is what made it easy to admit.

## Why these are committed

A rate in a terminal is a fact about one afternoon. A committed baseline is a fact you can diff.

The question worth answering is not "does RDK0007 fire on 0.4% of packages" but "**did that change,
and which packages moved**". That only works if the previous answer is written down, in a form
where a diff is readable.

## Regenerating

```console
dotnet run --project tools/Redecker.Corpus -c Release -- 500 results
dotnet run --project tools/Redecker.Corpus -c Release -- survey-tfm results
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
