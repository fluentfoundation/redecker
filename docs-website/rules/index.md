# Rules

| Code | Severity | Checks | Where |
| --- | --- | --- | --- |
| [RDK0001](./rdk0001) | error | Package MSBuild files reference files the package does not ship | `inspect` |
| [RDK0002](./rdk0002) | warning | An upgrade drops a `lib/` framework or a `runtimes/` RID | `inspect --from` |
| [RDK0003](./rdk0003) | error | A lockstep family is split across versions | `check`, MSBuild |
| [RDK0004](./rdk0004) | warning | A declared version no project references, carrying no hint | `check` |

## What they have in common

None of these break `dotnet restore`.

That is the whole point, and it is why a version-graph updater cannot see them. A dangling asset
reference ships a package that fails at build time on one target framework. A dropped runtime
identifier silently resolves to a different asset, or none. A split package family produces a
missing type or a provider that does not match its core package, at run time, in production. An
undocumented transitive pin constrains resolution forever for a reason nobody can name.

Every one of them restores perfectly.

## Severity

An **error** means something is broken and the finding is safe to gate on: `inspect` and `check`
exit non-zero, and the MSBuild task fails the build. A **warning** means something changed in a
way that deserves a human decision — dropping a runtime identifier is legitimate when nobody
targets it, and wrong when somebody does. Redecker cannot know which, so it says what happened
and leaves the judgement where it belongs.
