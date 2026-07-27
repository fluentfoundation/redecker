# Commands

| Command | Subject | Network |
| --- | --- | --- |
| `redecker inspect` | A package version | Yes |
| `redecker check` | The versions a repository declares | No |
| `redecker hints` | Pin rationales and their exit conditions | Only with `--check` |

## `redecker inspect`

Reads a package version and checks it for problems restore cannot see.

```console
redecker inspect <package> --to <version> [--from <version>]
```

Supplying `--from` enables the checks that only mean something across an upgrade, such as
[RDK0002](/rules/rdk0002).

```console
$ redecker inspect SQLitePCLRaw.lib.e_sqlite3 --from 2.1.11 --to 2.1.12
error RDK0001: buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets references
    runtimes/win-arm/native/e_sqlite3.dll, which the package does not contain
```

Exit code is 1 when any finding is an error, 0 otherwise, and 2 if the package could not be
found.

## `redecker check`

Checks that the versions a repository declares agree with each other. Runs entirely offline —
it reads MSBuild files and nothing else.

```console
redecker check [path]
```

`path` may be a project, a `Directory.Packages.props`, or a directory to search. Versions are
assembled across every file before being judged, because central package management declares
them in one place while projects may pin their own, and a family split across two files is
exactly the case worth catching.

## `redecker hints`

Lists the [pin hints](/concepts/pin-hints) a repository carries.

```console
redecker hints [path] [--check] [--github-token <token>]
```

Without `--check` it is a listing. With it, every recorded exit condition is re-evaluated and
pins that have outlived their reason are reported, with a non-zero exit so CI notices:

```console
$ redecker hints Directory.Packages.props --check
Directory.Packages.props:3 SQLitePCLRaw.bundle_e_sqlite3 2.1.11
    kind: UpstreamBug
    until: package-assets-intact(SQLitePCLRaw.lib.e_sqlite3@2.1.12)
    status: StillRequired - 2.1.12 still has 1 dangling asset reference(s)
```

`--github-token` is only needed for conditions that read an upstream issue tracker; it defaults
to `$GITHUB_TOKEN`. Without a token GitHub allows 60 requests an hour, which is not enough from
a shared CI address.
