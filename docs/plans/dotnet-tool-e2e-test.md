# Plan: Add dotnet tool E2E test for native AOT CLI

Tracking issue: TBD

## Problem

The existing CLI E2E tests install the native binary directly (mount or download), but none
test the **dotnet tool packaging** path. Since the dotnet tool now wraps a native AOT binary
via `SelfExtractingBundle`, we need a test that validates the full distribution chain:
`dotnet tool install` → self-extracting bundle extraction → `aspire new` + `aspire run`.

## Current State

### How CLI E2E tests work today

Tests run in Docker (`Dockerfile.e2e`) using one of three install modes
(`tests/Aspire.Cli.EndToEnd.Tests/Helpers/CliE2ETestHelpers.cs`):

| Mode | How CLI is installed | When used |
|------|---------------------|-----------|
| **SourceBuild** | Docker build stage runs `./build.sh --pack --bundle`, extracts native tar.gz to `/root/.aspire` | Local dev (no CI env vars) |
| **GaRelease** | Downloads from aspire.dev via `get-aspire-cli.sh` | CI scheduled runs |
| **PullRequest** | Downloads PR artifacts via `get-aspire-cli-pr.sh` | CI PR runs |

### How CI produces CLI artifacts

1. **`build-packages.yml`** — builds all managed NuGets, uploads as `built-nugets`.
   Uses `-p:SkipBundleDeps=true` which **removes Aspire.Cli.\*.csproj** from the build
   (see `eng/Build.props:48`). So **no CLI tool nupkg** is produced here.

2. **`build-cli-native-archives.yml`** — per-RID job (linux-x64, win-x64, osx-arm64):
   - Builds bundle payload (DCP + Dashboard + managed runtime)
   - Builds CLI via clipack projects (native AOT binary + tar.gz archive)
   - **Uploads only `aspire-cli*` archives** (tar.gz/zip), NOT the `Aspire.Cli.*.nupkg`
   - The `Aspire.Cli.linux-x64.*.nupkg` **is produced** but **not uploaded**

3. **`run-tests.yml`** — downloads `built-nugets` + `built-nugets-for-{rid}` into
   `artifacts/packages/`. Does **not** download `cli-native-archives-*`.

### The gap

- The tool nupkg (`Aspire.Cli.linux-x64.*.nupkg`) is built during the native archives job
  but **never uploaded** as a CI artifact (upload pattern `aspire-cli*` misses it).
- No test installs via `dotnet tool install` — all paths use the raw native binary.
- The self-extracting bundle flow (`BundleService.cs`) is untested end-to-end.

## Approach

No new install mode needed. The test class handles installation itself as part of the
test body — that's exactly what we're testing.

### Phase 1: Upload the tool nupkg from CI

**File**: `.github/workflows/build-cli-native-archives.yml`

Add a step (or expand the existing upload) to also upload the tool nupkg:

```yaml
- name: Upload CLI tool nupkg
  uses: actions/upload-artifact@...
  with:
    name: cli-tool-nupkg-${{ matrix.targets.rids }}
    path: artifacts/packages/**/Aspire.Cli.${{ matrix.targets.rids }}.*.nupkg
    if-no-files-found: error
```

### Phase 2: Make the nupkg available to E2E test jobs

**File**: `.github/workflows/run-tests.yml`

For test projects that `requiresCliArchive`, also download the tool nupkg artifact:

```yaml
- name: Download CLI tool nupkg
  if: ${{ inputs.requiresCliArchive }}
  uses: actions/download-artifact@...
  with:
    name: cli-tool-nupkg-linux-x64
    path: ${{ github.workspace }}/cli-tool-nupkg
```

### Phase 3: Write the test

Add a new test class (e.g., `DotnetToolSmokeTests.cs`) that:
1. Creates a Docker container with the nupkg directory mounted
2. Runs `dotnet tool install --global Aspire.Cli.linux-x64 --add-source /mounted/path/`
3. Verifies `aspire --version`
4. Runs `aspire new AspireStarterApp`
5. Runs `aspire run` and verifies startup
6. Sends Ctrl+C to stop

This mirrors the existing `SmokeTests.cs` flow but installs via `dotnet tool install`
instead of using a pre-installed native binary. Install the **RID-specific** package
directly (`Aspire.Cli.linux-x64`) since only that nupkg is available in the container.

### For local dev (SourceBuild)

The `Dockerfile.e2e` build stage already produces everything. Add one `COPY` line to
bring the nupkgs into the runtime stage:

```dockerfile
COPY --from=build /repo/artifacts/packages /opt/aspire-packages
```

Then the test installs from `/opt/aspire-packages/Release/Shipping/`.

## Key files

| File | Change |
|------|--------|
| `.github/workflows/build-cli-native-archives.yml` | Upload tool nupkg artifact |
| `.github/workflows/run-tests.yml` | Download tool nupkg for E2E jobs |
| `tests/Shared/Docker/Dockerfile.e2e` | COPY nupkgs into runtime stage (SourceBuild) |
| `tests/Aspire.Cli.EndToEnd.Tests/DotnetToolSmokeTests.cs` | New test class |

## Todos

- [x] Upload `Aspire.Cli.{rid}.*.nupkg` from `build-cli-native-archives.yml`
- [x] Download the nupkg in `run-tests.yml` for CLI E2E jobs
- [ ] Add `COPY --from=build` line to `Dockerfile.e2e` for local SourceBuild
- [ ] Write `DotnetToolSmokeTests.cs` — install via `dotnet tool install`, then `aspire new` + `aspire run`
- [ ] Verify it works locally with SourceBuild Docker mode
- [ ] Verify it works in CI with the uploaded nupkg artifact

## Status

**CI plumbing is committed** (nupkg upload + download). Test class and Dockerfile changes
deferred to a follow-up.
