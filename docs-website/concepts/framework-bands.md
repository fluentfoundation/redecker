# Framework Bands

A generic "bump to latest" is wrong for packages tied to a runtime generation. A project
targeting `net8.0` wants the 8.x line even when 9.x exists, because 9.x is written against a
runtime it is not running on.

The update unit is `(package, target framework band)`, not `(package)`.

## Which packages, exactly

This is policy, not physics. The tempting shortcut — treat all `Microsoft.Extensions.*` and
`System.*` as banded — is wrong **in both directions**, so Redecker states it as data you can
override.

### Banded

| Family | Why |
| --- | --- |
| `Microsoft.EntityFrameworkCore.*` | Providers and tools rely on runtime behaviour exclusive to the generation they ship with |
| `Microsoft.AspNetCore.OpenApi`, `.Diagnostics.EntityFrameworkCore`, `.Identity.EntityFrameworkCore` | Shipped outside the shared framework but written against a specific ASP.NET Core |
| `Microsoft.Extensions.Hosting`, `.DependencyInjection`, `.Configuration`, `.Http.Polly` | Pulling a 9.0 extension into a `net8.0` app lifts the assets out of the shared framework and ships them app-local and unoptimised |
| `System.Diagnostics.DiagnosticSource`, `System.Text.Json` | Deep runtime and serialization integration; a mismatch surfaces as missing types or contract differences |

### Not banded

Most of `Microsoft.Extensions.*` is compile-at-head. Caching, options, primitives and the
`.Abstractions` packages support older frameworks through netstandard2.0 and should simply take
the newest stable release. Holding them at 8.x achieves nothing.

Nor is every `System.*` package runtime-bound — `System.CommandLine` and `System.Linq.Async` are
ordinary libraries.

## The failure mode

Notice what unites the banded list: **none of these break restore.** They ship an unoptimised
asset, or surface a missing type at run time. The "lifting" case is the sharpest example — it
*works*, which is exactly what makes it easy to miss.

That is why a version-graph updater cannot catch any of it.

## Central package management makes this awkward

A `PackageVersion` is global, so the honest encoding of a per-framework constraint is a set of
target-framework-conditioned items:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.5" />
</ItemGroup>
```

Which is precisely the shape a naive updater flattens back into one.

## An empty band is a signal

When a banded package has no release in the band a project targets, Redecker reports nothing
rather than falling back to the newest version. Silently taking a 10.x package for a project
that cannot run it would be the exact bug this exists to prevent.

## Lockstep is a different constraint

Banding ties a package to the *framework*. [Lockstep](/rules/rdk0003) ties a set of packages to
*each other*. A family can be subject to both — EF Core is.
