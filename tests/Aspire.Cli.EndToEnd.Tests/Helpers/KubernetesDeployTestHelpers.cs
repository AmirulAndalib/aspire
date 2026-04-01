// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Hex1b.Automation;
using Xunit;

namespace Aspire.Cli.EndToEnd.Tests.Helpers;

/// <summary>
/// Shared helpers for Kubernetes deploy E2E tests that use KinD clusters with a local registry.
/// </summary>
internal static class KubernetesDeployTestHelpers
{
    private static string KindVersion => Environment.GetEnvironmentVariable("KIND_VERSION") ?? "v0.31.0";
    private static string HelmVersion => Environment.GetEnvironmentVariable("HELM_VERSION") ?? "v3.17.3";

    /// <summary>
    /// Generates a unique KinD cluster name (max 32 chars).
    /// </summary>
    internal static string GenerateUniqueClusterName() =>
        $"aspire-e2e-{Guid.NewGuid():N}"[..32];

    /// <summary>
    /// Installs KinD and Helm binaries to ~/.local/bin and adds to PATH.
    /// </summary>
    internal static async Task InstallKindAndHelmAsync(
        this Hex1bTerminalAutomator auto,
        SequenceCounter counter)
    {
        await auto.TypeAsync("mkdir -p ~/.local/bin");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync($"curl -sSLo ~/.local/bin/kind \"https://github.com/kubernetes-sigs/kind/releases/download/{KindVersion}/kind-linux-amd64\"");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(60));

        await auto.TypeAsync("chmod +x ~/.local/bin/kind");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync($"curl -sSL https://get.helm.sh/helm-{HelmVersion}-linux-amd64.tar.gz | tar xz -C /tmp");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(60));

        await auto.TypeAsync("mv /tmp/linux-amd64/helm ~/.local/bin/helm && rm -rf /tmp/linux-amd64");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync("export PATH=\"$HOME/.local/bin:$PATH\"");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync("kind version && helm version --short");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);
    }

    /// <summary>
    /// Creates a KinD cluster with a local Docker registry at localhost:5001.
    /// Follows the KinD local registry guide pattern.
    /// </summary>
    internal static async Task CreateKindClusterWithRegistryAsync(
        this Hex1bTerminalAutomator auto,
        SequenceCounter counter,
        string clusterName)
    {
        // Delete any leftover cluster with the same name
        await auto.TypeAsync($"kind delete cluster --name={clusterName} 2>/dev/null || true");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(60));

        // Start or reuse a local Docker registry at localhost:5001
        await auto.TypeAsync("docker inspect -f '{{.State.Running}}' kind-registry 2>/dev/null || docker run -d --restart=always -p 5001:5000 --network bridge --name kind-registry registry:2");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));

        // Create the cluster (no containerd config patches — registry is configured post-creation via hosts.toml)
        await auto.TypeAsync($"kind create cluster --name={clusterName} --wait=120s");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromMinutes(3));

        // Connect registry to cluster network
        await auto.TypeAsync($"docker network connect \"kind\" kind-registry 2>/dev/null || true");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Configure containerd on each node to resolve localhost:5001 via the registry container.
        // This uses the config_path approach required by containerd v2+ (shipped in KinD v0.31.0+).
        await auto.TypeAsync($"for node in $(kind get nodes --name={clusterName}); do " +
            "docker exec \"$node\" mkdir -p /etc/containerd/certs.d/localhost:5001 && " +
            "echo '[host.\"http://kind-registry:5000\"]' | docker exec -i \"$node\" tee /etc/containerd/certs.d/localhost:5001/hosts.toml > /dev/null && " +
            "echo '  capabilities = [\"pull\", \"resolve\"]' | docker exec -i \"$node\" tee -a /etc/containerd/certs.d/localhost:5001/hosts.toml > /dev/null; " +
            "done");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));

        // Create a ConfigMap so KinD knows about the local registry
        await auto.TypeAsync("cat > /tmp/local-registry-cm.yaml << 'CMEOF'");
        await auto.EnterAsync();
        await auto.TypeAsync("apiVersion: v1");
        await auto.EnterAsync();
        await auto.TypeAsync("kind: ConfigMap");
        await auto.EnterAsync();
        await auto.TypeAsync("metadata:");
        await auto.EnterAsync();
        await auto.TypeAsync("  name: local-registry-hosting");
        await auto.EnterAsync();
        await auto.TypeAsync("  namespace: kube-public");
        await auto.EnterAsync();
        await auto.TypeAsync("data:");
        await auto.EnterAsync();
        await auto.TypeAsync("  localRegistryHosting.v1: |");
        await auto.EnterAsync();
        await auto.TypeAsync("    host: \"localhost:5001\"");
        await auto.EnterAsync();
        await auto.TypeAsync("CMEOF");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        await auto.TypeAsync("kubectl apply -f /tmp/local-registry-cm.yaml");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Verify cluster is ready
        await auto.TypeAsync($"kubectl cluster-info --context kind-{clusterName}");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);
    }

    /// <summary>
    /// Scaffolds an Aspire AppHost project with a minimal API service on disk.
    /// Files are created directly for speed and determinism (no aspire new).
    /// </summary>
    internal static void ScaffoldK8sDeployProject(
        string workspaceRoot,
        string projectName,
        string[] appHostHostingPackages,
        string[] apiClientPackages,
        string appHostCode,
        string apiProgramCode)
    {
        var projectDir = Path.Combine(workspaceRoot, projectName);
        var appHostDir = Path.Combine(projectDir, $"{projectName}.AppHost");
        var apiDir = Path.Combine(projectDir, $"{projectName}.ApiService");
        var serviceDefaultsDir = Path.Combine(projectDir, $"{projectName}.ServiceDefaults");

        Directory.CreateDirectory(appHostDir);
        Directory.CreateDirectory(apiDir);
        Directory.CreateDirectory(serviceDefaultsDir);

        // Solution file — use non-interpolated string to avoid brace escaping issues with GUIDs
        var slnContent = """
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "APPHOST_NAME", "APPHOST_NAME\APPHOST_NAME.csproj", "{00000000-0000-0000-0000-000000000001}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "API_NAME", "API_NAME\API_NAME.csproj", "{00000000-0000-0000-0000-000000000002}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DEFAULTS_NAME", "DEFAULTS_NAME\DEFAULTS_NAME.csproj", "{00000000-0000-0000-0000-000000000003}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {00000000-0000-0000-0000-000000000001}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {00000000-0000-0000-0000-000000000001}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {00000000-0000-0000-0000-000000000001}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {00000000-0000-0000-0000-000000000001}.Release|Any CPU.Build.0 = Release|Any CPU
        {00000000-0000-0000-0000-000000000002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {00000000-0000-0000-0000-000000000002}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {00000000-0000-0000-0000-000000000002}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {00000000-0000-0000-0000-000000000002}.Release|Any CPU.Build.0 = Release|Any CPU
        {00000000-0000-0000-0000-000000000003}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {00000000-0000-0000-0000-000000000003}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {00000000-0000-0000-0000-000000000003}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {00000000-0000-0000-0000-000000000003}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal
""";
        slnContent = slnContent
            .Replace("APPHOST_NAME", $"{projectName}.AppHost")
            .Replace("API_NAME", $"{projectName}.ApiService")
            .Replace("DEFAULTS_NAME", $"{projectName}.ServiceDefaults");
        File.WriteAllText(Path.Combine(projectDir, $"{projectName}.sln"), slnContent);

        // AppHost csproj
        var hostingPackageRefs = string.Join(Environment.NewLine + "    ",
            appHostHostingPackages.Select(p => $"""<PackageReference Include="{p}" />"""));

        File.WriteAllText(Path.Combine(appHostDir, $"{projectName}.AppHost.csproj"), $"""
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="10.0.0-dev" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>{Guid.NewGuid()}</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    {hostingPackageRefs}
    <ProjectReference Include="..\{projectName}.ApiService\{projectName}.ApiService.csproj" />
    <ProjectReference Include="..\{projectName}.ServiceDefaults\{projectName}.ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
""");

        // AppHost code
        File.WriteAllText(Path.Combine(appHostDir, "AppHost.cs"), appHostCode);

        // ApiService csproj
        var clientPackageRefs = apiClientPackages.Length > 0
            ? string.Join(Environment.NewLine + "    ",
                apiClientPackages.Select(p => $"""<PackageReference Include="{p}" />"""))
            : "";

        var clientItemGroup = apiClientPackages.Length > 0
            ? $"""
  <ItemGroup>
    {clientPackageRefs}
    <ProjectReference Include="..\{projectName}.ServiceDefaults\{projectName}.ServiceDefaults.csproj" />
  </ItemGroup>
"""
            : $"""
  <ItemGroup>
    <ProjectReference Include="..\{projectName}.ServiceDefaults\{projectName}.ServiceDefaults.csproj" />
  </ItemGroup>
""";

        File.WriteAllText(Path.Combine(apiDir, $"{projectName}.ApiService.csproj"), $"""
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
{clientItemGroup}
</Project>
""");

        // ApiService Program.cs
        File.WriteAllText(Path.Combine(apiDir, "Program.cs"), apiProgramCode);

        // ServiceDefaults project
        File.WriteAllText(Path.Combine(serviceDefaultsDir, $"{projectName}.ServiceDefaults.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
  </ItemGroup>
</Project>
""");

        // ServiceDefaults Extensions.cs (minimal)
        File.WriteAllText(Path.Combine(serviceDefaultsDir, "Extensions.cs"), """
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
        return app;
    }
}
""");

        // aspire.config.json
        File.WriteAllText(Path.Combine(projectDir, "aspire.config.json"), """
{
  "sdk": {
    "version": "10.0.0-dev"
  }
}
""");
    }

    /// <summary>
    /// Runs <c>aspire deploy</c> interactively, answering parameter prompts via terminal automation.
    /// </summary>
    /// <param name="auto">The terminal automator.</param>
    /// <param name="counter">Sequence counter for prompt tracking.</param>
    /// <param name="parameterResponses">
    /// Ordered list of (promptSubstring, valueToType) tuples.
    /// Each entry matches by the parameter name appearing in the prompt text.
    /// Entries are consumed in order — first match wins.
    /// </param>
    /// <param name="outputDir">Optional output directory for publish artifacts.</param>
    internal static async Task AspireDeployInteractiveAsync(
        this Hex1bTerminalAutomator auto,
        SequenceCounter counter,
        IReadOnlyList<(string PromptText, string Value)> parameterResponses,
        string? outputDir = null)
    {
        var outputArg = outputDir is not null ? $" -o {outputDir}" : "";
        await auto.TypeAsync($"aspire deploy{outputArg}");
        await auto.EnterAsync();

        // Answer each parameter prompt in order.
        // The CLI shows parameter prompts via Spectre.Console TextPrompt with the parameter name as the label.
        // For multi-input forms, each input appears on its own line as "paramname: ".
        for (var i = 0; i < parameterResponses.Count; i++)
        {
            var (promptText, value) = parameterResponses[i];

            await auto.WaitUntilTextAsync(promptText, timeout: TimeSpan.FromMinutes(5));
            await auto.TypeAsync(value);
            await auto.EnterAsync();
        }

        // Wait for pipeline completion
        await auto.WaitUntilTextAsync("PIPELINE SUCCEEDED", timeout: TimeSpan.FromMinutes(10));
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Verifies a K8s deployment by port-forwarding and curling the test endpoint.
    /// Returns the curl output for assertion.
    /// </summary>
    internal static async Task VerifyDeploymentAsync(
        this Hex1bTerminalAutomator auto,
        SequenceCounter counter,
        string @namespace,
        string serviceName,
        int localPort,
        string testPath = "/test-deployment")
    {
        // Wait for all pods to be ready in the namespace
        await auto.TypeAsync($"kubectl wait --for=condition=Ready pod --all -n {@namespace} --timeout=180s");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromMinutes(4));

        // Show pod status for debugging
        await auto.TypeAsync($"kubectl get pods -n {@namespace}");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Port-forward in background
        await auto.TypeAsync($"kubectl port-forward -n {@namespace} svc/{serviceName}-service {localPort}:8080 &");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Brief pause for port-forward to establish
        await auto.TypeAsync("sleep 3");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);

        // Curl the test endpoint with retries, looking for "PASSED" in response body
        await auto.TypeAsync($"for i in $(seq 1 10); do " +
            $"result=$(curl -s http://localhost:{localPort}{testPath} 2>/dev/null); " +
            "if echo \"$result\" | grep -q 'PASSED'; then echo \"VERIFY_OK: $result\"; break; fi; " +
            "echo \"Attempt $i: got $result, retrying...\"; sleep 5; done");
        await auto.EnterAsync();

        // Wait for the VERIFY_OK marker to appear
        await auto.WaitUntilTextAsync("VERIFY_OK", timeout: TimeSpan.FromMinutes(2));
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(30));

        // Kill the port-forward background process
        await auto.TypeAsync("kill %1 2>/dev/null || true");
        await auto.EnterAsync();
        await auto.WaitForAnyPromptAsync(counter);
    }

    /// <summary>
    /// Cleans up a KinD cluster and registry (best-effort, in-terminal).
    /// </summary>
    internal static async Task CleanupKubernetesDeploymentAsync(
        this Hex1bTerminalAutomator auto,
        SequenceCounter counter,
        string clusterName)
    {
        await auto.TypeAsync($"kind delete cluster --name={clusterName} 2>/dev/null || true");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter, TimeSpan.FromSeconds(60));

        await auto.TypeAsync("docker rm -f kind-registry 2>/dev/null || true");
        await auto.EnterAsync();
        await auto.WaitForSuccessPromptAsync(counter);
    }

    /// <summary>
    /// Best-effort out-of-terminal cleanup for finally blocks.
    /// </summary>
    internal static async Task CleanupKindClusterOutOfBandAsync(string clusterName, ITestOutputHelper output)
    {
        try
        {
            using var kindProcess = new System.Diagnostics.Process();
            kindProcess.StartInfo.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kind");
            kindProcess.StartInfo.Arguments = $"delete cluster --name={clusterName}";
            kindProcess.StartInfo.RedirectStandardOutput = true;
            kindProcess.StartInfo.RedirectStandardError = true;
            kindProcess.StartInfo.UseShellExecute = false;
            kindProcess.Start();
            await kindProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            output.WriteLine($"Cleanup: KinD cluster '{clusterName}' deleted (exit code: {kindProcess.ExitCode})");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Cleanup: Failed to delete KinD cluster '{clusterName}': {ex.Message}");
        }

        try
        {
            using var registryProcess = new System.Diagnostics.Process();
            registryProcess.StartInfo.FileName = "docker";
            registryProcess.StartInfo.Arguments = "rm -f kind-registry";
            registryProcess.StartInfo.RedirectStandardOutput = true;
            registryProcess.StartInfo.RedirectStandardError = true;
            registryProcess.StartInfo.UseShellExecute = false;
            registryProcess.Start();
            await registryProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
