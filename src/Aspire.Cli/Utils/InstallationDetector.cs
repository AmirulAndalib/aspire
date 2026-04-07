// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Utils;

/// <summary>
/// Information about the CLI installation method and self-update availability.
/// </summary>
internal sealed record InstallationInfo(bool IsDotNetTool, bool SelfUpdateDisabled, string? UpdateInstructions);

/// <summary>
/// Detects how the CLI was installed and whether self-update is available.
/// </summary>
internal interface IInstallationDetector
{
    /// <summary>
    /// Gets information about the current CLI installation.
    /// </summary>
    InstallationInfo GetInstallationInfo();
}

/// <summary>
/// Model for the <c>.aspire-update.json</c> file that can disable self-update.
/// </summary>
internal sealed class AspireUpdateConfig
{
    [JsonPropertyName("selfUpdateDisabled")]
    public bool SelfUpdateDisabled { get; set; }

    [JsonPropertyName("updateInstructions")]
    public string? UpdateInstructions { get; set; }
}

/// <summary>
/// Detects CLI installation method by checking for <c>.aspire-update.json</c> and dotnet tool indicators.
/// </summary>
internal sealed class InstallationDetector : IInstallationDetector
{
    private readonly ILogger<InstallationDetector> _logger;
    private readonly string? _processPath;
    private InstallationInfo? _cachedInfo;

    internal const string UpdateConfigFileName = ".aspire-update.json";

    public InstallationDetector(ILogger<InstallationDetector> logger)
        : this(logger, Environment.ProcessPath)
    {
    }

    /// <summary>
    /// Constructor that accepts a process path for testability.
    /// </summary>
    internal InstallationDetector(ILogger<InstallationDetector> logger, string? processPath)
    {
        _logger = logger;
        _processPath = processPath;
    }

    public InstallationInfo GetInstallationInfo()
    {
        if (_cachedInfo is not null)
        {
            return _cachedInfo;
        }

        _cachedInfo = DetectInstallation();
        return _cachedInfo;
    }

    private InstallationInfo DetectInstallation()
    {
        // Check if running as a dotnet tool first
        if (IsDotNetToolProcess(_processPath))
        {
            _logger.LogDebug("CLI is running as a .NET tool.");
            return new InstallationInfo(IsDotNetTool: true, SelfUpdateDisabled: false, UpdateInstructions: null);
        }

        // Check for .aspire-update.json next to the resolved process path
        var config = TryLoadUpdateConfig(_processPath);
        if (config is not null)
        {
            if (config.SelfUpdateDisabled)
            {
                _logger.LogDebug("Self-update is disabled via {FileName}.", UpdateConfigFileName);
                return new InstallationInfo(IsDotNetTool: false, SelfUpdateDisabled: true, UpdateInstructions: config.UpdateInstructions);
            }

            _logger.LogDebug("{FileName} found but selfUpdateDisabled is false.", UpdateConfigFileName);
        }

        // Default: script install or direct binary, self-update is available
        return new InstallationInfo(IsDotNetTool: false, SelfUpdateDisabled: false, UpdateInstructions: null);
    }

    private static bool IsDotNetToolProcess(string? processPath)
    {
        if (string.IsNullOrEmpty(processPath))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(processPath);
        return string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase);
    }

    private AspireUpdateConfig? TryLoadUpdateConfig(string? processPath)
    {
        if (string.IsNullOrEmpty(processPath))
        {
            return null;
        }

        try
        {
            // Resolve symlinks (critical for Homebrew on macOS where the binary is symlinked).
            // File.ResolveLinkTarget returns null for non-symlinks (not an error).
            // On Linux, Environment.ProcessPath reads /proc/self/exe (already resolved).
            var resolvedPath = processPath;
            try
            {
                var linkTarget = File.ResolveLinkTarget(processPath, returnFinalTarget: true);
                if (linkTarget is not null)
                {
                    resolvedPath = linkTarget.FullName;
                    _logger.LogDebug("Resolved symlink {ProcessPath} -> {ResolvedPath}", processPath, resolvedPath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Failed to resolve symlink for {ProcessPath}, using original path.", processPath);
            }

            var directory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var configPath = Path.Combine(directory, UpdateConfigFileName);
            if (!File.Exists(configPath))
            {
                _logger.LogDebug("{FileName} not found at {ConfigPath}.", UpdateConfigFileName, configPath);
                return null;
            }

            _logger.LogDebug("Found {FileName} at {ConfigPath}.", UpdateConfigFileName, configPath);

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, JsonSourceGenerationContext.Default.AspireUpdateConfig);

            if (config is null)
            {
                // Null deserialization result (e.g., "null" literal in JSON) — fail closed
                _logger.LogWarning("Failed to parse {FileName}: deserialized to null. Treating as self-update disabled.", UpdateConfigFileName);
                return new AspireUpdateConfig { SelfUpdateDisabled = true };
            }

            return config;
        }
        catch (JsonException ex)
        {
            // Malformed JSON — fail closed (safer for package managers)
            _logger.LogWarning(ex, "Failed to parse {FileName}. Treating as self-update disabled.", UpdateConfigFileName);
            return new AspireUpdateConfig { SelfUpdateDisabled = true };
        }
        catch (IOException ex)
        {
            // File read error — fail closed
            _logger.LogWarning(ex, "Failed to read {FileName}. Treating as self-update disabled.", UpdateConfigFileName);
            return new AspireUpdateConfig { SelfUpdateDisabled = true };
        }
        catch (UnauthorizedAccessException ex)
        {
            // Permission error — fail closed
            _logger.LogWarning(ex, "Failed to read {FileName}. Treating as self-update disabled.", UpdateConfigFileName);
            return new AspireUpdateConfig { SelfUpdateDisabled = true };
        }
    }
}
