# MSBuild Integration

```console
dotnet add package Redecker.MSBuild
```

The package runs the coherence checks during every build. It ships no library — it is build
logic, marked as a development dependency, so it never becomes part of what your package
requires of its consumers.

## What it does

Today it runs [RDK0003](/rules/rdk0003): a family that must move in lockstep but has been split
across versions fails the build.

```
error RDK0003: Microsoft.EntityFrameworkCore* packages are split across 2 versions: 9.0.0, 9.0.5.
Every package in this family must carry the same version. Declared:
Microsoft.EntityFrameworkCore 9.0.5, Microsoft.EntityFrameworkCore.SqlServer 9.0.0.
```

It reads the items MSBuild has already evaluated rather than parsing `Directory.Packages.props`
itself, so conditioned and imported declarations are seen exactly as the build sees them.

## Options

| Property | Default | Effect |
| --- | --- | --- |
| `RedeckerCheckEnabled` | `true` | Set to `false` to skip the check entirely |
| `RedeckerTreatAsError` | `true` | Set to `false` to report findings as warnings |

```xml
<PropertyGroup>
  <RedeckerTreatAsError>false</RedeckerTreatAsError>
</PropertyGroup>
```

::: warning
Downgrading to a warning is a reasonable first step when adopting this on an existing repository
with an existing split. It is a poor resting place: this whole project exists because warnings
nobody reads are how a broken upgrade reaches production.
:::

## Stating your own families

The default policy covers `Microsoft.EntityFrameworkCore`. Add your own:

```xml
<ItemGroup>
  <RedeckerLockstepPrefix Include="Contoso.Data" />
</ItemGroup>
```

Declaring any `RedeckerLockstepPrefix` item replaces the default, so include EF Core explicitly
if you still want it:

```xml
<ItemGroup>
  <RedeckerLockstepPrefix Include="Microsoft.EntityFrameworkCore" />
  <RedeckerLockstepPrefix Include="Contoso.Data" />
</ItemGroup>
```

## Compatibility

The task is compiled for `netstandard2.0`, so one asset loads in both the .NET Framework MSBuild
that Visual Studio uses and the .NET one behind `dotnet build`. It carries no dependencies of its
own: an MSBuild task is loaded into a long-lived host that may already have other versions of the
same assemblies loaded, and every dependency a task drags in is a chance to break somebody else's
build.
