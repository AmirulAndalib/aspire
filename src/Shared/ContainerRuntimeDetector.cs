// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Shared container runtime detection logic mirroring the approach used by DCP:
//   https://github.com/microsoft/dcp/blob/main/internal/containers/runtimes/runtime.go
//   https://github.com/microsoft/dcp/blob/main/internal/containers/flags/container_runtime.go
//
// Detection strategy (matches DCP's FindAvailableContainerRuntime):
//   1. If a runtime is explicitly configured, use it directly.
//   2. Otherwise, probe all known runtimes in parallel.
//   3. Prefer installed+running over installed-only over not-found.
//   4. When runtimes are equally available, prefer the default (Docker).

using System.Diagnostics;

namespace Aspire.Hosting;

/// <summary>
/// Describes the availability of a single container runtime (e.g., Docker or Podman).
/// </summary>
internal sealed class ContainerRuntimeInfo
{
    /// <summary>
    /// The executable name (e.g., "docker", "podman").
    /// </summary>
    public required string Executable { get; init; }

    /// <summary>
    /// Display name (e.g., "Docker", "Podman").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the runtime CLI was found on PATH.
    /// </summary>
    public bool IsInstalled { get; init; }

    /// <summary>
    /// Whether the runtime daemon/service is responding.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Whether this is the default runtime when all else is equal.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Error message if detection failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether the runtime is fully operational.
    /// </summary>
    public bool IsHealthy => IsInstalled && IsRunning;
}

/// <summary>
/// Detects available container runtimes by probing CLI executables on PATH.
/// Mirrors the detection logic used by DCP.
/// </summary>
internal static class ContainerRuntimeDetector
{
    private static readonly TimeSpan s_processTimeout = TimeSpan.FromSeconds(10);

    private static readonly (string Executable, string Name, bool IsDefault)[] s_knownRuntimes =
    [
        ("docker", "Docker", true),
        ("podman", "Podman", false)
    ];

    /// <summary>
    /// Finds the best available container runtime, optionally using an explicit preference.
    /// </summary>
    /// <param name="configuredRuntime">
    /// An explicitly configured runtime name (e.g., "docker" or "podman" from ASPIRE_CONTAINER_RUNTIME).
    /// When set, only that runtime is checked. When null, all known runtimes are probed in parallel.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The best available runtime, or null if no runtime was found.
    /// When a runtime is configured but not available, returns its info with <see cref="ContainerRuntimeInfo.IsInstalled"/> = false.
    /// </returns>
    public static async Task<ContainerRuntimeInfo?> FindAvailableRuntimeAsync(string? configuredRuntime = null, CancellationToken cancellationToken = default)
    {
        if (configuredRuntime is not null)
        {
            // Explicit config: check only the requested runtime
            var known = s_knownRuntimes.FirstOrDefault(r => string.Equals(r.Executable, configuredRuntime, StringComparison.OrdinalIgnoreCase));
            var name = known.Name ?? configuredRuntime;
            var isDefault = known.IsDefault;
            return await CheckRuntimeAsync(configuredRuntime, name, isDefault, cancellationToken).ConfigureAwait(false);
        }

        // Auto-detect: probe all runtimes in parallel (matches DCP behavior)
        var tasks = s_knownRuntimes.Select(r =>
            CheckRuntimeAsync(r.Executable, r.Name, r.IsDefault, cancellationToken)).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Pick the best runtime using DCP's priority:
        // 1. Prefer installed over not-installed
        // 2. Prefer running over not-running
        // 3. Prefer the default runtime when all else is equal
        ContainerRuntimeInfo? best = null;
        foreach (var candidate in results)
        {
            if (best is null)
            {
                best = candidate;
                continue;
            }

            if (!best.IsInstalled && candidate.IsInstalled)
            {
                best = candidate;
            }
            else if (!best.IsRunning && candidate.IsRunning)
            {
                best = candidate;
            }
            else if (candidate.IsDefault
                && candidate.IsInstalled == best.IsInstalled
                && candidate.IsRunning == best.IsRunning)
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Checks the availability of a specific container runtime.
    /// </summary>
    public static async Task<ContainerRuntimeInfo> CheckRuntimeAsync(string executable, string name, bool isDefault, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if the CLI is installed by running `<runtime> container ls -n 1`
            // This matches DCP's check and also validates the daemon is running.
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "container ls -n 1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ContainerRuntimeInfo
                {
                    Executable = executable,
                    Name = name,
                    IsInstalled = false,
                    IsRunning = false,
                    IsDefault = isDefault,
                    Error = $"{name} CLI not found on PATH."
                };
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(s_processTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(); } catch { /* best effort */ }
                return new ContainerRuntimeInfo
                {
                    Executable = executable,
                    Name = name,
                    IsInstalled = true,
                    IsRunning = false,
                    IsDefault = isDefault,
                    Error = $"{name} CLI timed out while checking status."
                };
            }

            if (process.ExitCode == 0)
            {
                return new ContainerRuntimeInfo
                {
                    Executable = executable,
                    Name = name,
                    IsInstalled = true,
                    IsRunning = true,
                    IsDefault = isDefault
                };
            }

            // Non-zero exit code: CLI exists (we started it) but daemon may not be running.
            // Try a simpler check to distinguish "not installed" from "not running"
            var isInstalled = await IsCliInstalledAsync(executable, cancellationToken).ConfigureAwait(false);

            return new ContainerRuntimeInfo
            {
                Executable = executable,
                Name = name,
                IsInstalled = isInstalled,
                IsRunning = false,
                IsDefault = isDefault,
                Error = isInstalled
                    ? $"{name} is installed but the daemon is not running."
                    : $"{name} CLI not found on PATH."
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // Process.Start throws Win32Exception when the executable is not found
            return new ContainerRuntimeInfo
            {
                Executable = executable,
                Name = name,
                IsInstalled = false,
                IsRunning = false,
                IsDefault = isDefault,
                Error = $"{name} CLI not found on PATH."
            };
        }
    }

    private static async Task<bool> IsCliInstalledAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(s_processTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(); } catch { /* best effort */ }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }
}
