// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.EndToEnd.Tests.Helpers;
using Aspire.Cli.Tests.Utils;
using Hex1b.Automation;
using Xunit;

namespace Aspire.Cli.EndToEnd.Tests;

/// <summary>
/// End-to-end tests for the embedded CliChannel feature.
/// Verifies that binaries built with a CliChannel value (e.g., "local") auto-resolve
/// their channel without needing an explicit <c>aspire config set channel</c> call.
/// </summary>
public sealed class EmbeddedChannelTests(ITestOutputHelper output)
{
    /// <summary>
    /// Verifies that a LocalHive-installed CLI works without manually setting the channel in global config.
    /// When the CLI is built with <c>/p:CliChannel=local</c>, it should automatically resolve the "local"
    /// channel from the embedded assembly metadata, making <c>aspire config set channel local -g</c> unnecessary.
    /// </summary>
    [Fact]
    public async Task LocalHive_WorksWithoutExplicitChannelConfig()
    {
        var repoRoot = CliE2ETestHelpers.GetRepoRoot();
        var strategy = CliInstallStrategy.Detect();

        // This test only makes sense for LocalHive mode where the binary has CliChannel=local embedded
        Assert.SkipUnless(strategy.Mode == CliInstallMode.LocalHive,
            "This test requires a LocalHive archive (set ASPIRE_E2E_ARCHIVE). " +
            "The binary must be built with /p:CliChannel=local to have the embedded channel.");

        var workspace = TemporaryWorkspace.Create(output);

        using var terminal = CliE2ETestHelpers.CreateDockerTestTerminal(repoRoot, strategy, output, workspace: workspace);

        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareDockerEnvironmentAsync(counter, workspace);

        // Install the CLI manually WITHOUT calling 'aspire config set channel local -g'.
        // This is the key difference from InstallAspireCliAsync which does set the channel.
        await auto.TypeAsync("mkdir -p ~/.aspire && tar -xzf /tmp/aspire-localhive.tar.gz -C ~/.aspire");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));
        await auto.TypeAsync("export PATH=~/.aspire/bin:$PATH");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify no channel is set in global config
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire config get channel -g");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the CLI works — aspire --version should succeed
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire --version");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Exit the shell
        await auto.TypeAsync("exit");
        await auto.EnterAsync();

        await pendingRun;
    }

    /// <summary>
    /// Verifies that an explicit global channel config takes priority over the embedded channel.
    /// Even when the binary has CliChannel=local embedded, setting <c>aspire config set channel staging -g</c>
    /// should override the embedded value.
    /// </summary>
    [Fact]
    public async Task GlobalConfig_OverridesEmbeddedChannel()
    {
        var repoRoot = CliE2ETestHelpers.GetRepoRoot();
        var strategy = CliInstallStrategy.Detect();

        Assert.SkipUnless(strategy.Mode == CliInstallMode.LocalHive,
            "This test requires a LocalHive archive (set ASPIRE_E2E_ARCHIVE). " +
            "The binary must be built with /p:CliChannel=local to have the embedded channel.");

        var workspace = TemporaryWorkspace.Create(output);

        using var terminal = CliE2ETestHelpers.CreateDockerTestTerminal(repoRoot, strategy, output, workspace: workspace);

        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareDockerEnvironmentAsync(counter, workspace);

        // Install without channel config (just like the test above)
        await auto.TypeAsync("mkdir -p ~/.aspire && tar -xzf /tmp/aspire-localhive.tar.gz -C ~/.aspire");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));
        await auto.TypeAsync("export PATH=~/.aspire/bin:$PATH");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Explicitly set a different channel in global config
        await auto.TypeAsync("aspire config set channel staging -g");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the global config has the overridden channel
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire config get channel -g");
        await auto.EnterAsync();
        await auto.WaitUntilAsync(
            s => s.ContainsText("staging"),
            timeout: TimeSpan.FromSeconds(10),
            description: "global channel config shows 'staging'");
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the CLI still works with the config-override channel
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire --version");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Exit the shell
        await auto.TypeAsync("exit");
        await auto.EnterAsync();

        await pendingRun;
    }

    /// <summary>
    /// Simulates the upgrade scenario: a config channel from a previous install ("stable") is already
    /// present, and then a new binary with an embedded channel ("local") is installed on top.
    /// Verifies that the explicit config channel takes precedence over the embedded one, and that
    /// the CLI remains functional after the simulated upgrade.
    /// </summary>
    [Fact]
    public async Task PreExistingConfigChannel_TakesPrecedenceOverEmbedded()
    {
        var repoRoot = CliE2ETestHelpers.GetRepoRoot();
        var strategy = CliInstallStrategy.Detect();

        Assert.SkipUnless(strategy.Mode == CliInstallMode.LocalHive,
            "This test requires a LocalHive archive (set ASPIRE_E2E_ARCHIVE). " +
            "The binary must be built with /p:CliChannel=local to have the embedded channel.");

        var workspace = TemporaryWorkspace.Create(output);

        using var terminal = CliE2ETestHelpers.CreateDockerTestTerminal(repoRoot, strategy, output, workspace: workspace);

        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareDockerEnvironmentAsync(counter, workspace);

        // Pre-seed a global config with channel "stable" (simulates a previous install)
        await auto.TypeAsync("mkdir -p ~/.aspire && echo '{\"channel\":\"stable\"}' > ~/.aspire/aspire.config.json");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Now install the new binary (which has CliChannel=local embedded)
        await auto.TypeAsync("tar -xzf /tmp/aspire-localhive.tar.gz -C ~/.aspire");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));
        await auto.TypeAsync("export PATH=~/.aspire/bin:$PATH");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the config still has "stable" (not overwritten by embedded "local")
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire config get channel -g");
        await auto.EnterAsync();
        await auto.WaitUntilAsync(
            s => s.ContainsText("stable"),
            timeout: TimeSpan.FromSeconds(10),
            description: "global config channel is still 'stable' after binary upgrade");
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the CLI works — the "stable" config channel should take precedence
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire --version");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Now remove the config channel to verify fallback to embedded "local"
        await auto.TypeAsync("aspire config delete channel -g");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the CLI still works — should fall back to embedded channel
        await auto.ClearScreenAsync(counter);
        await auto.TypeAsync("aspire --version");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Exit the shell
        await auto.TypeAsync("exit");
        await auto.EnterAsync();

        await pendingRun;
    }
}
