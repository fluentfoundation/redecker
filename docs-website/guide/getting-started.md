# Getting Started

Redecker ships as two packages, and you probably want both for different reasons.

| Package | What it is | When you want it |
| --- | --- | --- |
| [`dotnet-redecker`](https://www.nuget.org/packages/dotnet-redecker) | A .NET global tool | Investigating a specific upgrade, or running checks in CI |
| [`Redecker.MSBuild`](https://www.nuget.org/packages/Redecker.MSBuild) | Build-time checks | Making a repository's version coherence a build failure |

## Install the tool

```console
dotnet tool install --global dotnet-redecker
```

Then point it at an upgrade you are considering:

```console
redecker inspect Newtonsoft.Json --from 13.0.1 --to 13.0.3
```

It exits non-zero if anything is an error, so it works directly as a gate in a workflow step
without any extra scripting.

## Add the build-time checks

```console
dotnet add package Redecker.MSBuild
```

That is all. From the next build, a package family that must move together but has been split
across versions fails the build:

```
error RDK0003: Microsoft.EntityFrameworkCore* packages are split across 2 versions: 9.0.0, 9.0.5
```

See [MSBuild integration](./msbuild) for how to make it a warning instead, or to state your own
families.

## What Redecker is not

It does not resolve your dependency graph, and it will not replace `dotnet restore` or your
existing updater. It answers a narrower question those tools cannot: **is this upgrade sound in
ways that restore succeeding does not prove?**

If you have not read [the problems](/problems), start there — it is the argument for why that
question is worth a tool at all.

It also has nothing useful to say about [epoch changes](/concepts/epochs) like
`xunit` → `xunit.v3`, which is a limit of the ecosystem rather than a gap in the roadmap.
