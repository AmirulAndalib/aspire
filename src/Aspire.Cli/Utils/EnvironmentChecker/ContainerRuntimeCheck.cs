// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Utils.EnvironmentChecker;

/// <summary>
/// Checks if a container runtime (Docker or Podman) is available and running.
/// </summary>
internal sealed partial class ContainerRuntimeCheck(ILogger<ContainerRuntimeCheck> logger) : IEnvironmentCheck
{
    private static readonly TimeSpan s_processTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Minimum Docker version required for Aspire.
    /// </summary>
    public const string MinimumDockerVersion = "28.0.0";

    /// <summary>
    /// Minimum Podman version required for Aspire.
    /// </summary>
    public const string MinimumPodmanVersion = "5.0.0";

    public int Order => 40; // Process check - more expensive

    public async Task<IReadOnlyList<EnvironmentCheckResult>> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use shared detector to probe both runtimes in parallel
            var dockerInfo = await ContainerRuntimeDetector.CheckRuntimeAsync("docker", "Docker", isDefault: true, cancellationToken);
            var podmanInfo = await ContainerRuntimeDetector.CheckRuntimeAsync("podman", "Podman", isDefault: false, cancellationToken);

            // If Docker is healthy, do extended checks (version, Windows containers, tunnel)
            if (dockerInfo.IsHealthy)
            {
                var result = await CheckDockerExtendedAsync(cancellationToken);
                if (result is not null)
                {
                    return [result];
                }
            }

            // If Podman is healthy, do version check
            if (podmanInfo.IsHealthy)
            {
                var result = await CheckPodmanExtendedAsync(cancellationToken);
                if (result is not null)
                {
                    return [result];
                }
            }

            // Prefer healthy Docker
            if (dockerInfo.IsHealthy)
            {
                return [PassResult("Docker detected and running")];
            }

            // Prefer healthy Podman
            if (podmanInfo.IsHealthy)
            {
                return [PassResult("Podman detected and running")];
            }

            // If Docker is installed but not running, prefer showing that error
            if (dockerInfo.IsInstalled)
            {
                return [WarningResult(
                    "Docker is installed but not running",
                    GetContainerRuntimeStartupAdvice("Docker"))];
            }

            // If Podman is installed but not running, show that
            if (podmanInfo.IsInstalled)
            {
                return [WarningResult(
                    "Podman is installed but not running",
                    GetContainerRuntimeStartupAdvice("Podman"))];
            }

            // Neither found
            return [new EnvironmentCheckResult
            {
                Category = "container",
                Name = "container-runtime",
                Status = EnvironmentCheckStatus.Fail,
                Message = "No container runtime detected",
                Fix = "Install Docker Desktop: https://www.docker.com/products/docker-desktop or Podman: https://podman.io/getting-started/installation",
                Link = "https://aka.ms/dotnet/aspire/containers"
            }];
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking container runtime");
            return [new EnvironmentCheckResult
            {
                Category = "container",
                Name = "container-runtime",
                Status = EnvironmentCheckStatus.Fail,
                Message = "Failed to check container runtime",
                Details = ex.Message
            }];
        }
    }

    private async Task<EnvironmentCheckResult?> CheckDockerExtendedAsync(CancellationToken cancellationToken)
    {
        return await CheckVersionAndModeAsync("Docker", cancellationToken);
    }

    private async Task<EnvironmentCheckResult?> CheckPodmanExtendedAsync(CancellationToken cancellationToken)
    {
        return await CheckVersionAndModeAsync("Podman", cancellationToken);
    }

    private async Task<EnvironmentCheckResult?> CheckVersionAndModeAsync(string runtime, CancellationToken cancellationToken)
    {
        try
        {
            var runtimeLower = runtime.ToLowerInvariant();
            var versionProcessInfo = new ProcessStartInfo
            {
                FileName = runtimeLower,
                Arguments = "version -f json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var versionProcess = Process.Start(versionProcessInfo);
            if (versionProcess is null)
            {
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(s_processTimeout);

            string versionOutput;
            try
            {
                versionOutput = await versionProcess.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                await versionProcess.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                versionProcess.Kill();
                return null;
            }

            var versionInfo = ContainerVersionInfo.Parse(versionOutput);
            var clientVersion = versionInfo.ClientVersion ?? ParseVersionFromOutput(versionOutput);
            var context = versionInfo.Context;
            var serverOs = versionInfo.ServerOs;
            var isDockerDesktop = runtime == "Docker" &&
                context is not null &&
                context.Contains("desktop", StringComparison.OrdinalIgnoreCase);

            var minimumVersion = GetMinimumVersion(runtime);

            // Check minimum version
            if (clientVersion is not null && minimumVersion is not null && clientVersion < minimumVersion)
            {
                return WarningResult(
                    $"{runtime} version {clientVersion} is below minimum required {GetMinimumVersionString(runtime)}",
                    GetContainerRuntimeUpgradeAdvice(runtime));
            }

            // Docker-specific: check Windows container mode
            if (runtime == "Docker" && string.Equals(serverOs, "windows", StringComparison.OrdinalIgnoreCase))
            {
                return new EnvironmentCheckResult
                {
                    Category = "container",
                    Name = "container-runtime",
                    Status = EnvironmentCheckStatus.Fail,
                    Message = $"{(isDockerDesktop ? "Docker Desktop" : "Docker")} is running in Windows container mode",
                    Details = "Aspire requires Linux containers. Windows containers are not supported.",
                    Fix = "Switch Docker Desktop to Linux containers mode (right-click Docker tray icon → 'Switch to Linux containers...')",
                    Link = "https://aka.ms/dotnet/aspire/containers"
                };
            }

            // Docker Engine (not Desktop): check tunnel
            if (runtime == "Docker" && !isDockerDesktop)
            {
                var tunnelEnabled = Environment.GetEnvironmentVariable("ASPIRE_ENABLE_CONTAINER_TUNNEL");
                var versionSuffix = clientVersion is not null ? $" (version {clientVersion})" : "";
                if (!string.Equals(tunnelEnabled, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return new EnvironmentCheckResult
                    {
                        Category = "container",
                        Name = "container-runtime",
                        Status = EnvironmentCheckStatus.Warning,
                        Message = $"Docker Engine detected{versionSuffix}. Aspire's container tunnel is required to allow containers to reach applications running on the host",
                        Fix = "Set environment variable: ASPIRE_ENABLE_CONTAINER_TUNNEL=true",
                        Link = "https://aka.ms/aspire-prerequisites#docker-engine"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error during extended {Runtime} check", runtime);
        }

        return null; // No issues found
    }

    private static EnvironmentCheckResult PassResult(string message) => new()
    {
        Category = "container",
        Name = "container-runtime",
        Status = EnvironmentCheckStatus.Pass,
        Message = message
    };

    private static EnvironmentCheckResult WarningResult(string message, string fix) => new()
    {
        Category = "container",
        Name = "container-runtime",
        Status = EnvironmentCheckStatus.Warning,
        Message = message,
        Fix = fix,
        Link = "https://aka.ms/dotnet/aspire/containers"
    };

    /// <summary>
    /// Parses a version number from container runtime output as a fallback when JSON parsing fails.
    /// </summary>
    internal static Version? ParseVersionFromOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // Match version patterns like "20.10.17", "4.3.1", "27.5.1" etc.
        // The pattern looks for "version" followed by a version number
        var match = VersionRegex().Match(output);
        if (match.Success && Version.TryParse(match.Groups[1].Value, out var version))
        {
            return version;
        }

        return null;
    }

    /// <summary>
    /// Gets the minimum required version for the specified container runtime.
    /// </summary>
    private static Version? GetMinimumVersion(string runtime)
    {
        var versionString = GetMinimumVersionString(runtime);

        if (versionString is not null && Version.TryParse(versionString, out var version))
        {
            return version;
        }

        return null;
    }

    /// <summary>
    /// Gets the minimum required version string for the specified container runtime.
    /// </summary>
    private static string? GetMinimumVersionString(string runtime)
    {
        return runtime switch
        {
            "Docker" => MinimumDockerVersion,
            "Podman" => MinimumPodmanVersion,
            _ => null
        };
    }

    private static string GetContainerRuntimeUpgradeAdvice(string runtime)
    {
        return runtime switch
        {
            "Docker" => $"Upgrade Docker to version {MinimumDockerVersion} or later from: https://www.docker.com/products/docker-desktop",
            "Podman" => $"Upgrade Podman to version {MinimumPodmanVersion} or later from: https://podman.io/getting-started/installation",
            _ => $"Upgrade {runtime} to a newer version"
        };
    }

    [GeneratedRegex(@"version\s+(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    private static string GetContainerRuntimeStartupAdvice(string runtime, bool isDockerDesktop = false)
    {
        return runtime switch
        {
            "Docker" when isDockerDesktop => "Start Docker Desktop",
            "Docker" => "Start Docker daemon",
            "Podman" => "Start Podman service: sudo systemctl start podman",
            _ => $"Start {runtime} daemon"
        };
    }
}

/// <summary>
/// Parsed container runtime version information.
/// </summary>
internal sealed record ContainerVersionInfo(
    Version? ClientVersion,
    Version? ServerVersion,
    string? Context,
    string? ServerOs)
{
    /// <summary>
    /// Parses container version info from 'docker/podman version -f json' output.
    /// </summary>
    public static ContainerVersionInfo Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new ContainerVersionInfo(null, null, null, null);
        }

        try
        {
            var json = JsonSerializer.Deserialize(output, JsonSourceGenerationContext.Default.ContainerVersionJson);
            if (json is null)
            {
                return new ContainerVersionInfo(null, null, null, null);
            }

            Version.TryParse(json.Client?.Version, out var clientVersion);
            Version.TryParse(json.Server?.Version, out var serverVersion);

            return new ContainerVersionInfo(
                clientVersion,
                serverVersion,
                json.Client?.Context,
                json.Server?.Os);
        }
        catch (JsonException)
        {
            return new ContainerVersionInfo(null, null, null, null);
        }
    }
}

/// <summary>
/// JSON structure for container runtime version output.
/// </summary>
internal sealed class ContainerVersionJson
{
    [JsonPropertyName("Client")]
    public ContainerClientJson? Client { get; set; }

    [JsonPropertyName("Server")]
    public ContainerServerJson? Server { get; set; }
}

/// <summary>
/// JSON structure for the Client section of container runtime version output.
/// </summary>
internal sealed class ContainerClientJson
{
    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("Context")]
    public string? Context { get; set; }
}

/// <summary>
/// JSON structure for the Server section of container runtime version output.
/// </summary>
internal sealed class ContainerServerJson
{
    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("Os")]
    public string? Os { get; set; }
}
