// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.EndToEnd.Tests.Helpers;
using Aspire.Cli.Tests.Utils;
using Hex1b.Automation;
using Xunit;

namespace Aspire.Cli.EndToEnd.Tests;

/// <summary>
/// End-to-end tests for the aspire otel logs command with structured logs.
/// Each test class runs as a separate CI job for parallelization.
/// </summary>
public sealed class OtelLogsTests(ITestOutputHelper output)
{
    [Fact]
    [CaptureWorkspaceOnFailure]
    public async Task OtelLogsReturnsStructuredLogsFromStarterApp()
    {
        var repoRoot = CliE2ETestHelpers.GetRepoRoot();
        var installMode = CliE2ETestHelpers.DetectDockerInstallMode(repoRoot);

        var workspace = TemporaryWorkspace.Create(output);

        using var terminal = CliE2ETestHelpers.CreateDockerTestTerminal(repoRoot, installMode, output, mountDockerSocket: true, workspace: workspace);

        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareDockerEnvironmentAsync(counter, workspace);
        await auto.InstallAspireCliInDockerAsync(installMode, counter);

        // Create a new Starter project (includes an ASP.NET Core apiservice)
        await auto.AspireNewAsync("AspireOtelLogsApp", counter);

        // Navigate to the AppHost directory
        await auto.TypeAsync("cd AspireOtelLogsApp/AspireOtelLogsApp.AppHost");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Start the AppHost in the background
        await auto.AspireStartAsync(counter);

        // Wait for resources to produce structured logs
        await auto.TypeAsync("sleep 15");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Run aspire otel logs and capture output to a file
        await auto.TypeAsync("aspire otel logs > otel_logs.txt 2>&1");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify the output contains structured log entries (not "No logs found")
        await auto.TypeAsync("cat otel_logs.txt | head -20");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Assert that logs were found by checking absence of "No logs found"
        await auto.TypeAsync("grep -c 'No logs found' otel_logs.txt && echo 'NO_LOGS_DETECTED' || echo 'STRUCTURED_LOGS_PRESENT'");
        await auto.EnterAsync();
        await auto.WaitUntilTextAsync("STRUCTURED_LOGS_PRESENT", timeout: TimeSpan.FromSeconds(10));
        await auto.WaitForAnyPromptAsync(counter);

        // Verify the output contains entries from the apiservice resource
        await auto.TypeAsync("grep -q 'apiservice' otel_logs.txt && echo 'APISERVICE_LOGS_FOUND' || echo 'APISERVICE_LOGS_MISSING'");
        await auto.EnterAsync();
        await auto.WaitUntilTextAsync("APISERVICE_LOGS_FOUND", timeout: TimeSpan.FromSeconds(10));
        await auto.WaitForAnyPromptAsync(counter);

        // Also verify JSON format works and contains structured data
        await auto.TypeAsync("aspire otel logs --format json > otel_logs_json.txt 2>&1");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify JSON output contains resourceLogs key
        await auto.TypeAsync("grep -q 'resourceLogs' otel_logs_json.txt && echo 'JSON_STRUCTURED_LOGS_PRESENT' || echo 'JSON_STRUCTURED_LOGS_MISSING'");
        await auto.EnterAsync();
        await auto.WaitUntilTextAsync("JSON_STRUCTURED_LOGS_PRESENT", timeout: TimeSpan.FromSeconds(10));
        await auto.WaitForAnyPromptAsync(counter);

        // Stop the AppHost
        await auto.AspireStopAsync(counter);

        // Exit the shell
        await auto.TypeAsync("exit");
        await auto.EnterAsync();

        await pendingRun;
    }
}
