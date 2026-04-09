// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;

namespace Aspire.Cli.Documentation.Docs;

/// <summary>
/// Resolves configuration for the Aspire llms.txt docs source.
/// </summary>
internal static class DocsSourceConfiguration
{
    private const string IndexCacheKeyPrefix = "index:";

    /// <summary>
    /// Configuration key for overriding the llms.txt source URL.
    /// </summary>
    public const string LlmsTxtUrlConfigKey = "Aspire:Cli:Docs:LlmsTxtUrl";

    /// <summary>
    /// Default URL for the abridged Aspire llms.txt documentation source.
    /// </summary>
    public const string DefaultLlmsTxtUrl = "https://aspire.dev/llms-small.txt";

    /// <summary>
    /// Gets the URL used to fetch the abridged Aspire llms.txt documentation source.
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <returns>The resolved documentation source URL.</returns>
    public static string GetLlmsTxtUrl(IConfiguration configuration)
        => configuration[LlmsTxtUrlConfigKey] ?? DefaultLlmsTxtUrl;

    /// <summary>
    /// Gets a source-specific cache key for the parsed llms.txt index.
    /// </summary>
    /// <param name="llmsTxtUrl">The configured documentation source URL.</param>
    /// <returns>The cache key used for the parsed documentation index.</returns>
    public static string GetIndexCacheKey(string llmsTxtUrl)
        => $"{IndexCacheKeyPrefix}{GetContentCacheKey(llmsTxtUrl)}";

    /// <summary>
    /// Gets the cache key used for the fetched llms.txt source content.
    /// </summary>
    /// <param name="llmsTxtUrl">The configured documentation source URL.</param>
    /// <returns>The cache key used for source content and ETag persistence.</returns>
    public static string GetContentCacheKey(string llmsTxtUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(llmsTxtUrl);

        var trimmedUrl = llmsTxtUrl.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            var fallback = Path.GetFileNameWithoutExtension(trimmedUrl);
            return string.IsNullOrWhiteSpace(fallback) ? "llms" : fallback;
        }

        var rawSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathSegments = new List<string>(rawSegments.Length);
        for (var i = 0; i < rawSegments.Length; i++)
        {
            var segment = i == rawSegments.Length - 1
                ? Path.GetFileNameWithoutExtension(rawSegments[i])
                : rawSegments[i];

            if (!string.IsNullOrWhiteSpace(segment))
            {
                pathSegments.Add(segment);
            }
        }

        var stem = pathSegments.Count > 0 ? string.Join('-', pathSegments) : "llms";
        if (uri.Host.Equals("aspire.dev", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort)
        {
            return stem;
        }

        return $"{uri.Host}{(uri.IsDefaultPort ? "" : $"-{uri.Port}")}-{stem}";
    }
}
