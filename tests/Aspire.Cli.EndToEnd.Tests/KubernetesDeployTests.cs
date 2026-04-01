// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.EndToEnd.Tests.Helpers;
using Aspire.Cli.Tests.Utils;
using Aspire.TestUtilities;
using Hex1b.Automation;
using Xunit;

namespace Aspire.Cli.EndToEnd.Tests;

/// <summary>
/// End-to-end tests for <c>aspire deploy</c> to Kubernetes via Helm.
/// Tests the interactive deploy workflow: scaffold project, run <c>aspire deploy</c>,
/// answer parameter prompts, verify pod health, and validate via <c>/test-deployment</c> endpoint.
/// Each test class runs as a separate CI job for parallelization.
/// </summary>
public sealed class KubernetesDeployTests(ITestOutputHelper output)
{
    private const string ProjectName = "K8sDeployTest";

    [Fact]
    [QuarantinedTest("https://github.com/microsoft/aspire/issues/15511")]
    [CaptureWorkspaceOnFailure]
    public async Task DeployBasicApiService()
    {
        using var workspace = TemporaryWorkspace.Create(output);

        var prNumber = CliE2ETestHelpers.GetRequiredPrNumber();
        var commitSha = CliE2ETestHelpers.GetRequiredCommitSha();
        var isCI = CliE2ETestHelpers.IsRunningInCI;
        var clusterName = KubernetesDeployTestHelpers.GenerateUniqueClusterName();
        var k8sNamespace = $"test-{clusterName[..16]}";

        output.WriteLine($"Cluster name: {clusterName}");
        output.WriteLine($"Namespace: {k8sNamespace}");

        using var terminal = CliE2ETestHelpers.CreateTestTerminal();
        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        // Prepare environment
        await auto.PrepareEnvironmentAsync(workspace, counter);

        if (isCI)
        {
            await auto.InstallAspireCliFromPullRequestAsync(prNumber, counter);
            await auto.SourceAspireCliEnvironmentAsync(counter);
            await auto.VerifyAspireCliVersionAsync(commitSha, counter);
        }

        try
        {
            // =====================================================================
            // Phase 1: Install KinD + Helm, create cluster with local registry
            // =====================================================================

            await auto.InstallKindAndHelmAsync(counter);
            await auto.CreateKindClusterWithRegistryAsync(counter, clusterName);

            // =====================================================================
            // Phase 2: Scaffold the project on disk
            // =====================================================================

            var appHostCode = $$"""
                using Aspire.Hosting;
                using Aspire.Hosting.Kubernetes;

                var builder = DistributedApplication.CreateBuilder(args);

                var registryEndpoint = builder.AddParameter("registryendpoint");
                var registry = builder.AddContainerRegistry("registry", registryEndpoint);

                var api = builder.AddProject<Projects.{{ProjectName}}_ApiService>("server")
                    .WithExternalHttpEndpoints();

                builder.AddKubernetesEnvironment("env")
                    .WithHelm(helm =>
                    {
                        helm.WithNamespace(builder.AddParameter("namespace"));
                        helm.WithChartVersion(builder.AddParameter("chartversion"));
                    });

                builder.Build().Run();
                """;

            var apiProgramCode = """
                var builder = WebApplication.CreateBuilder(args);
                builder.AddServiceDefaults();

                var app = builder.Build();
                app.MapDefaultEndpoints();

                app.MapGet("/test-deployment", () =>
                {
                    return Results.Ok("PASSED: basic API service is running");
                });

                app.Run();
                """;

            KubernetesDeployTestHelpers.ScaffoldK8sDeployProject(
                workspace.WorkspaceRoot.FullName,
                ProjectName,
                appHostHostingPackages: ["Aspire.Hosting.Kubernetes"],
                apiClientPackages: [],
                appHostCode: appHostCode,
                apiProgramCode: apiProgramCode);

            // Navigate into the project directory
            await auto.TypeAsync($"cd {ProjectName}");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            // Verify scaffold
            await auto.TypeAsync($"ls -la {ProjectName}.AppHost/");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            // =====================================================================
            // Phase 3: Unset ASPIRE_PLAYGROUND and run aspire deploy interactively
            // =====================================================================

            await auto.TypeAsync("unset ASPIRE_PLAYGROUND");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            // The deploy will prompt for:
            // 1. registryendpoint - the container registry (localhost:5001 for KinD local registry)
            // 2. namespace - the K8s namespace
            // 3. chartversion - the Helm chart version
            // Parameters are prompted in alphabetical order by name in a multi-input form.
            await auto.AspireDeployInteractiveAsync(
                counter,
                parameterResponses:
                [
                    ("chartversion", "0.1.0"),
                    ("namespace", k8sNamespace),
                    ("registryendpoint", "localhost:5001"),
                ]);

            // =====================================================================
            // Phase 4: Verify the deployment
            // =====================================================================

            await auto.VerifyDeploymentAsync(
                counter,
                @namespace: k8sNamespace,
                serviceName: "server",
                localPort: 18080,
                testPath: "/test-deployment");

            // =====================================================================
            // Phase 5: Cleanup
            // =====================================================================

            await auto.CleanupKubernetesDeploymentAsync(counter, clusterName);

            await auto.TypeAsync("exit");
            await auto.EnterAsync();
        }
        finally
        {
            await KubernetesDeployTestHelpers.CleanupKindClusterOutOfBandAsync(clusterName, output);
        }

        await pendingRun;
    }

    [Fact]
    [QuarantinedTest("https://github.com/microsoft/aspire/issues/15511")]
    [CaptureWorkspaceOnFailure]
    public async Task DeployWithRedis()
    {
        using var workspace = TemporaryWorkspace.Create(output);

        var prNumber = CliE2ETestHelpers.GetRequiredPrNumber();
        var commitSha = CliE2ETestHelpers.GetRequiredCommitSha();
        var isCI = CliE2ETestHelpers.IsRunningInCI;
        var clusterName = KubernetesDeployTestHelpers.GenerateUniqueClusterName();
        var k8sNamespace = $"test-{clusterName[..16]}";

        output.WriteLine($"Cluster name: {clusterName}");
        output.WriteLine($"Namespace: {k8sNamespace}");

        using var terminal = CliE2ETestHelpers.CreateTestTerminal();
        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareEnvironmentAsync(workspace, counter);

        if (isCI)
        {
            await auto.InstallAspireCliFromPullRequestAsync(prNumber, counter);
            await auto.SourceAspireCliEnvironmentAsync(counter);
            await auto.VerifyAspireCliVersionAsync(commitSha, counter);
        }

        try
        {
            await auto.InstallKindAndHelmAsync(counter);
            await auto.CreateKindClusterWithRegistryAsync(counter, clusterName);

            // Scaffold: AppHost with Redis + API service that uses Redis client
            var appHostCode = $$"""
                using Aspire.Hosting;
                using Aspire.Hosting.Kubernetes;

                var builder = DistributedApplication.CreateBuilder(args);

                var registryEndpoint = builder.AddParameter("registryendpoint");
                var registry = builder.AddContainerRegistry("registry", registryEndpoint);

                var cache = builder.AddRedis("cache");

                var api = builder.AddProject<Projects.{{ProjectName}}_ApiService>("server")
                    .WithReference(cache)
                    .WaitFor(cache)
                    .WithExternalHttpEndpoints();

                builder.AddKubernetesEnvironment("env")
                    .WithHelm(helm =>
                    {
                        helm.WithNamespace(builder.AddParameter("namespace"));
                        helm.WithChartVersion(builder.AddParameter("chartversion"));
                    });

                builder.Build().Run();
                """;

            var apiProgramCode = """
                using StackExchange.Redis;

                var builder = WebApplication.CreateBuilder(args);
                builder.AddServiceDefaults();
                builder.AddRedisClient("cache");

                var app = builder.Build();
                app.MapDefaultEndpoints();

                app.MapGet("/test-deployment", async (IConnectionMultiplexer redis) =>
                {
                    var db = redis.GetDatabase();

                    // Write a value
                    var testKey = $"test-{Guid.NewGuid():N}";
                    await db.StringSetAsync(testKey, "hello-from-k8s");

                    // Read it back
                    var value = await db.StringGetAsync(testKey);

                    // Cleanup
                    await db.KeyDeleteAsync(testKey);

                    if (value == "hello-from-k8s")
                    {
                        return Results.Ok("PASSED: Redis SET+GET works");
                    }
                    return Results.Problem($"FAILED: expected 'hello-from-k8s', got '{value}'");
                });

                app.Run();
                """;

            KubernetesDeployTestHelpers.ScaffoldK8sDeployProject(
                workspace.WorkspaceRoot.FullName,
                ProjectName,
                appHostHostingPackages: ["Aspire.Hosting.Kubernetes", "Aspire.Hosting.Redis"],
                apiClientPackages: ["Aspire.StackExchange.Redis"],
                appHostCode: appHostCode,
                apiProgramCode: apiProgramCode);

            await auto.TypeAsync($"cd {ProjectName}");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            await auto.TypeAsync("unset ASPIRE_PLAYGROUND");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            // Deploy prompts: chartversion, namespace, registryendpoint
            // Redis also generates a password parameter (cache_password)
            await auto.AspireDeployInteractiveAsync(
                counter,
                parameterResponses:
                [
                    ("chartversion", "0.1.0"),
                    ("namespace", k8sNamespace),
                    ("registryendpoint", "localhost:5001"),
                ]);

            await auto.VerifyDeploymentAsync(
                counter,
                @namespace: k8sNamespace,
                serviceName: "server",
                localPort: 18081,
                testPath: "/test-deployment");

            await auto.CleanupKubernetesDeploymentAsync(counter, clusterName);

            await auto.TypeAsync("exit");
            await auto.EnterAsync();
        }
        finally
        {
            await KubernetesDeployTestHelpers.CleanupKindClusterOutOfBandAsync(clusterName, output);
        }

        await pendingRun;
    }

    [Fact]
    [QuarantinedTest("https://github.com/microsoft/aspire/issues/15511")]
    [CaptureWorkspaceOnFailure]
    public async Task DeployWithPostgres()
    {
        using var workspace = TemporaryWorkspace.Create(output);

        var prNumber = CliE2ETestHelpers.GetRequiredPrNumber();
        var commitSha = CliE2ETestHelpers.GetRequiredCommitSha();
        var isCI = CliE2ETestHelpers.IsRunningInCI;
        var clusterName = KubernetesDeployTestHelpers.GenerateUniqueClusterName();
        var k8sNamespace = $"test-{clusterName[..16]}";

        output.WriteLine($"Cluster name: {clusterName}");
        output.WriteLine($"Namespace: {k8sNamespace}");

        using var terminal = CliE2ETestHelpers.CreateTestTerminal();
        var pendingRun = terminal.RunAsync(TestContext.Current.CancellationToken);

        var counter = new SequenceCounter();
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(500));

        await auto.PrepareEnvironmentAsync(workspace, counter);

        if (isCI)
        {
            await auto.InstallAspireCliFromPullRequestAsync(prNumber, counter);
            await auto.SourceAspireCliEnvironmentAsync(counter);
            await auto.VerifyAspireCliVersionAsync(commitSha, counter);
        }

        try
        {
            await auto.InstallKindAndHelmAsync(counter);
            await auto.CreateKindClusterWithRegistryAsync(counter, clusterName);

            var appHostCode = $$"""
                using Aspire.Hosting;
                using Aspire.Hosting.Kubernetes;

                var builder = DistributedApplication.CreateBuilder(args);

                var registryEndpoint = builder.AddParameter("registryendpoint");
                var registry = builder.AddContainerRegistry("registry", registryEndpoint);

                var postgres = builder.AddPostgres("pg").AddDatabase("testdb");

                var api = builder.AddProject<Projects.{{ProjectName}}_ApiService>("server")
                    .WithReference(postgres)
                    .WaitFor(postgres)
                    .WithExternalHttpEndpoints();

                builder.AddKubernetesEnvironment("env")
                    .WithHelm(helm =>
                    {
                        helm.WithNamespace(builder.AddParameter("namespace"));
                        helm.WithChartVersion(builder.AddParameter("chartversion"));
                    });

                builder.Build().Run();
                """;

            var apiProgramCode = """
                using Npgsql;

                var builder = WebApplication.CreateBuilder(args);
                builder.AddServiceDefaults();
                builder.AddNpgsqlDataSource("testdb");

                var app = builder.Build();
                app.MapDefaultEndpoints();

                app.MapGet("/test-deployment", async (NpgsqlDataSource dataSource) =>
                {
                    await using var conn = await dataSource.OpenConnectionAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1 AS result";
                    var result = await cmd.ExecuteScalarAsync();

                    if (result is int val && val == 1)
                    {
                        return Results.Ok("PASSED: PostgreSQL SELECT 1 works");
                    }
                    return Results.Problem($"FAILED: expected 1, got '{result}'");
                });

                app.Run();
                """;

            KubernetesDeployTestHelpers.ScaffoldK8sDeployProject(
                workspace.WorkspaceRoot.FullName,
                ProjectName,
                appHostHostingPackages: ["Aspire.Hosting.Kubernetes", "Aspire.Hosting.PostgreSQL"],
                apiClientPackages: ["Aspire.Npgsql"],
                appHostCode: appHostCode,
                apiProgramCode: apiProgramCode);

            await auto.TypeAsync($"cd {ProjectName}");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            await auto.TypeAsync("unset ASPIRE_PLAYGROUND");
            await auto.EnterAsync();
            await auto.WaitForSuccessPromptAsync(counter);

            await auto.AspireDeployInteractiveAsync(
                counter,
                parameterResponses:
                [
                    ("chartversion", "0.1.0"),
                    ("namespace", k8sNamespace),
                    ("registryendpoint", "localhost:5001"),
                ]);

            await auto.VerifyDeploymentAsync(
                counter,
                @namespace: k8sNamespace,
                serviceName: "server",
                localPort: 18082,
                testPath: "/test-deployment");

            await auto.CleanupKubernetesDeploymentAsync(counter, clusterName);

            await auto.TypeAsync("exit");
            await auto.EnterAsync();
        }
        finally
        {
            await KubernetesDeployTestHelpers.CleanupKindClusterOutOfBandAsync(clusterName, output);
        }

        await pendingRun;
    }
}
