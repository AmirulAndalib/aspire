// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Documentation.ApiDocs;

/// <summary>
/// Service for fetching Aspire API reference content.
/// </summary>
internal interface IApiDocsFetcher
{
    /// <summary>
    /// Fetches the sitemap used to build the API catalog.
    /// </summary>
    Task<string?> FetchSitemapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches markdown content for the given API page URL.
    /// </summary>
    Task<string?> FetchPageAsync(string pageUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IApiDocsFetcher"/> with cache-backed ETag support.
/// </summary>
internal sealed class ApiDocsFetcher(HttpClient httpClient, IApiDocsCache cache, IConfiguration configuration, ILogger<ApiDocsFetcher> logger) : IApiDocsFetcher
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IApiDocsCache _cache = cache;
    private readonly string _sitemapUrl = ApiDocsSourceConfiguration.GetSitemapUrl(configuration);
    private readonly string _sitemapCacheKey = ApiDocsSourceConfiguration.GetSitemapContentCacheKey(ApiDocsSourceConfiguration.GetSitemapUrl(configuration));
    private readonly ILogger<ApiDocsFetcher> _logger = logger;

    /// <summary>
    /// Fetches the sitemap used to build the API catalog.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sitemap content, or <c>null</c> when it cannot be retrieved.</returns>
    public Task<string?> FetchSitemapAsync(CancellationToken cancellationToken = default)
        => FetchWithCacheKeyAsync(_sitemapUrl, _sitemapCacheKey, cancellationToken);

    /// <summary>
    /// Fetches markdown content for the specified API page.
    /// </summary>
    /// <param name="pageUrl">The canonical API page URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The markdown content, or <c>null</c> when it cannot be retrieved.</returns>
    public Task<string?> FetchPageAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        var markdownUrl = ApiDocsSourceConfiguration.BuildMarkdownUrl(pageUrl, _sitemapUrl);
        var cacheKey = ApiDocsSourceConfiguration.GetPageContentCacheKey(pageUrl, _sitemapUrl);
        return FetchWithCacheKeyAsync(markdownUrl, cacheKey, cancellationToken);
    }

    private async Task<string?> FetchWithCacheKeyAsync(string url, string cacheKey, CancellationToken cancellationToken)
    {
        await MigrateLegacyCacheAsync(url, cacheKey, cancellationToken).ConfigureAwait(false);
        return await CachedHttpDocumentFetcher.FetchAsync(_httpClient, _cache, url, cacheKey, _logger, cancellationToken).ConfigureAwait(false);
    }

    private async Task MigrateLegacyCacheAsync(string legacyKey, string cacheKey, CancellationToken cancellationToken)
    {
        if (string.Equals(legacyKey, cacheKey, StringComparison.Ordinal))
        {
            return;
        }

        var currentContent = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        var currentETag = await _cache.GetETagAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (currentContent is not null || currentETag is not null)
        {
            await ClearLegacyCacheAsync(legacyKey, cancellationToken).ConfigureAwait(false);
            return;
        }

        var legacyContent = await _cache.GetAsync(legacyKey, cancellationToken).ConfigureAwait(false);
        var legacyETag = await _cache.GetETagAsync(legacyKey, cancellationToken).ConfigureAwait(false);

        if (legacyContent is not null)
        {
            await _cache.SetAsync(cacheKey, legacyContent, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(legacyETag))
        {
            await _cache.SetETagAsync(cacheKey, legacyETag, cancellationToken).ConfigureAwait(false);
        }

        if (legacyContent is not null || !string.IsNullOrEmpty(legacyETag))
        {
            await ClearLegacyCacheAsync(legacyKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ClearLegacyCacheAsync(string legacyKey, CancellationToken cancellationToken)
    {
        await _cache.InvalidateAsync(legacyKey, cancellationToken).ConfigureAwait(false);
        await _cache.SetETagAsync(legacyKey, null, cancellationToken).ConfigureAwait(false);
    }
}
