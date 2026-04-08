// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.EndToEnd.Tests.Helpers;
using Aspire.Cli.Tests.Utils;
using Hex1b.Automation;
using Hex1b.Input;
using Xunit;

namespace Aspire.Cli.EndToEnd.Tests;

/// <summary>
/// End-to-end test that validates the dotnet tool packaging path:
/// installs the CLI via <c>dotnet tool install</c> from a locally-built nupkg,
/// then creates and runs an Aspire project.
/// </summary>
public sealed class DotnetToolSmokeTests(ITestOutputHelper output)
{
    private const string ContainerNupkgDir = "/opt/aspire-tool-packages";

    [Fact]
    public async Task CreateAndRunAspireStarterProjectViaDotnetTool()
    {
        var repoRoot = CliE2ETestHelpers.GetRepoRoot();
        var installMode = CliE2ETestHelpers.DetectDockerInstallMode(repoRoot);

        // Find the nupkg directory — either from CI env var or local artifacts.
        var hostNupkgDir = FindCliToolNupkgDir(repoRoot);
        Assert.SkipWhen(hostNupkgDir is null,
            "No CLI tool nupkg for linux-x64 found. " +
            "Set ASPIRE_CLI_TOOL_NUPKG_DIR or build with: ./build.sh --pack --bundle");

        output.WriteLine($"CLI tool nupkg dir: {hostNupkgDir}");

        using var workspace = TemporaryWorkspace.Create(output);

        using var terminal = CliE2ETestHelpers.CreateDockerTestTerminal(
            repoRoot,
            installMode,
            output,
            mountDockerSocket: true,
            workspace: workspace,
            additionalVolumes: [$"{hostNupkgDir}:{ContainerNupkgDir}:ro"]);

        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareDockerEnvironmentAsync(counter, workspace);

        // Install the CLI via dotnet tool install (instead of InstallAspireCliInDockerAsync)
        await InstallCliViaDotnetToolAsync(auto, counter);

        await auto.AspireNewAsync("AspireStarterApp", counter);

        await auto.TypeAsync("aspire run");
        await auto.EnterAsync();

        await auto.WaitUntilAsync(s =>
        {
            if (s.ContainsText("Select an AppHost to use:"))
            {
                throw new InvalidOperationException(
                    "Unexpected apphost selection prompt detected! " +
                    "This indicates multiple apphosts were incorrectly detected.");
            }
            return s.ContainsText("Press CTRL+C to stop the AppHost and exit.");
        }, timeout: TimeSpan.FromMinutes(2), description: "Press CTRL+C message (aspire run started)");

        await auto.Ctrl().KeyAsync(Hex1bKey.C);
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync("exit");
        await auto.EnterAsync();

        await pendingRun;
    }

    /// <summary>
    /// Installs the Aspire CLI inside the container using <c>dotnet tool install</c>
    /// from the nupkg directory mounted at <see cref="ContainerNupkgDir"/>.
    /// </summary>
    private static async Task InstallCliViaDotnetToolAsync(
        Hex1bTerminalAutomator auto,
        SequenceCounter counter)
    {
        // Extract version from the nupkg filename and install as a global tool.
        var installCmd =
            $"""nupkg=$(find {ContainerNupkgDir} -name "Aspire.Cli.linux-x64.*.nupkg" | head -1) && """
            + """version=$(basename "$nupkg" | sed 's/Aspire\.Cli\.linux-x64\.\(.*\)\.nupkg/\1/') && """
            + $"""dotnet tool install --global Aspire.Cli.linux-x64 --add-source "$(dirname "$nupkg")" --version "$version" """;

        await auto.TypeAsync(installCmd);
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(120));

        // Ensure dotnet tools dir is on PATH
        await auto.TypeAsync("export PATH=\"$HOME/.dotnet/tools:$PATH\"");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the tool is available
        await auto.TypeAsync("aspire --version");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Finds the directory containing the linux-x64 CLI tool nupkg.
    /// Checks <c>ASPIRE_CLI_TOOL_NUPKG_DIR</c> env var first (CI), then local artifacts.
    /// </summary>
    private static string? FindCliToolNupkgDir(string repoRoot)
    {
        // CI: the nupkg is downloaded to a known directory
        var envDir = Environment.GetEnvironmentVariable("ASPIRE_CLI_TOOL_NUPKG_DIR");
        if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
        {
            return envDir;
        }

        // Local: search artifacts/packages for the linux-x64 tool nupkg
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        if (!Directory.Exists(packagesDir))
        {
            return null;
        }

        var nupkg = Directory.GetFiles(packagesDir, "Aspire.Cli.linux-x64.*.nupkg", SearchOption.AllDirectories)
            .FirstOrDefault();

        return nupkg is not null ? Path.GetDirectoryName(nupkg) : null;
    }
}
