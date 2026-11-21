# Continuous integration

## Workflow

`.github/workflows/ci.yml` runs on every push to `master`, every pull request, and on demand
(`workflow_dispatch`). It executes one matrix job, `build-test`, on `ubuntu-latest` and
`windows-latest`:

| Step | Command |
|---|---|
| Restore | `dotnet restore OpenGothic.MonoGame.sln` |
| Build | `dotnet build OpenGothic.MonoGame.sln -c Release --no-restore` |
| Test | `dotnet test OpenGothic.MonoGame.sln -c Release --no-restore --no-build` |

`fail-fast` is off so a Linux failure does not hide the Windows result. Runs on the same ref
cancel each other, so a new push supersedes an in-flight run.

## Why both platforms

Both legs build **and** test the **whole** solution, including the DesktopGL host. This was
verified by running the exact three commands on a clean Linux tree: restore, build and test all
succeed, and `OpenGothic.Console` compiles. Do not downgrade the Linux job to build-only without
re-checking — the host does not need a display or a Gothic installation to *build* or to run the
tests, because nothing opens a window during tests.

`ZenKit` ships a native `libzenkitcapi.so` for `linux-x64`, which is why the Linux leg works at all.
Anything that later genuinely needs a GPU, a display server or game data must be skipped in CI, not
silently tolerated.

## Package cache

`actions/setup-dotnet` caches the NuGet global-packages folder, keyed on `Directory.Build.props` and
every `*.csproj`. Both files are listed because that is where all package versions live: editing a
version in `Directory.Build.props` must produce a new cache key, otherwise CI would restore a stale
graph.

## Keeping actions current

Actions are pinned to major versions (`actions/checkout@v7`, `actions/setup-dotnet@v6`). Major
pins get fixes without breaking changes; bumping a major is a deliberate edit. Verify an action
input actually exists before adding it — a mistyped `with:` key fails the run only after it starts.

## Local reproduction

CI uses no secrets or environment variables beyond `DOTNET_NOLOGO` and
`DOTNET_CLI_TELEMETRY_OPTOUT`, so any failure reproduces locally:

```bash
dotnet restore OpenGothic.MonoGame.sln
dotnet build OpenGothic.MonoGame.sln -c Release --no-restore
dotnet test OpenGothic.MonoGame.sln -c Release --no-restore --no-build
```

Keep the `--no-restore` / `--no-build` flags when reproducing a failure; without them a stale local
`obj/` or `bin/` can mask the real problem. In particular, do not judge the Linux leg from a tree
that Windows has already built: the shared `obj/` holds Windows-specific restore data and the build
fails with a confusing "fallback package folder" error. Copy the sources to a clean directory
instead.
