# Plan: Add dotnet tool E2E test for native AOT CLI

Tracking issue: TBD

## Problem

The existing CLI E2E tests install the native binary directly (mount or download), but none test the **dotnet tool packaging** path. Since the dotnet tool now wraps a native AOT binary via `SelfExtractingBundle`, we need a test that validates the full distribution chain.

## Gap

No test currently verifies:
1. `dotnet tool install` of the packed `.nupkg` works
2. The self-extracting bundle extracts and runs correctly
3. `aspire new` + `aspire run` works through the dotnet-tool entry point

## Approach

Add a new E2E test (or a new install mode in the existing framework) that:
1. Installs the CLI via `dotnet tool install --global Aspire.Cli --add-source <local-nupkg-path>`
2. Verifies `aspire --version` works
3. Runs `aspire new` + `aspire run` (or `aspire start`/`aspire stop`)
4. Validates the app starts successfully

### Key files

- `tests/Aspire.Cli.EndToEnd.Tests/Helpers/CliE2ETestHelpers.cs` — add 4th install mode (`DotnetTool`)
- `tests/Aspire.Cli.EndToEnd.Tests/SmokeTests.cs` — reference test for `aspire new` + `aspire run`
- `src/Aspire.Cli/Aspire.Cli.csproj` — dotnet tool + native AOT config
- `src/Aspire.Cli/Bundles/BundleService.cs` — self-extracting bundle logic

### Todos

- [ ] Add a 4th install mode (`DotnetTool`) to `CliE2ETestHelpers` alongside SourceBuild/GaRelease/PullRequest
- [ ] Create a test that uses `dotnet tool install` from local nupkg in Docker
- [ ] Run the standard smoke test flow (`aspire new` + `aspire run`) through that path
- [ ] Ensure CI can produce the nupkg artifact and feed it to the test
