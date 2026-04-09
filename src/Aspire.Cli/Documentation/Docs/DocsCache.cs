// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aspire.Cli.Documentation.Docs;

/// <summary>
/// Cache for aspire.dev documentation content with ETag support.
/// Uses both in-memory cache for fast access and disk cache for persistence across CLI invocations.
/// </summary>
internal sealed class DocsCache : IDocsCache
{
    private const string DocsCacheSubdirectory = "docs";

    private readonly FileBackedDocumentContentCache _contentCache;
    private readonly string _llmsTxtUrl;
    private readonly string _sourceCacheKey;
    private readonly string _indexCacheKey;
    private readonly string _indexSourceFingerprintCacheKey;

    public DocsCache(
        IMemoryCache memoryCache,
        CliExecutionContext executionContext,
        IConfiguration configuration,
        ILogger<DocsCache> logger)
    {
        _llmsTxtUrl = DocsSourceConfiguration.GetLlmsTxtUrl(configuration);
        _sourceCacheKey = DocsSourceConfiguration.GetContentCacheKey(_llmsTxtUrl);
        _indexCacheKey = DocsSourceConfiguration.GetIndexCacheKey(_llmsTxtUrl);
        _indexSourceFingerprintCacheKey = $"{_indexCacheKey}:fingerprint";
        _contentCache = new FileBackedDocumentContentCache(memoryCache, executionContext, DocsCacheSubdirectory, logger);
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => _contentCache.GetAsync(key, cancellationToken);

    public Task SetAsync(string key, string content, CancellationToken cancellationToken = default)
        => _contentCache.SetAsync(key, content, cancellationToken);

    public Task<string?> GetETagAsync(string url, CancellationToken cancellationToken = default)
        => _contentCache.GetETagAsync(url, cancellationToken);

    public Task SetETagAsync(string url, string? etag, CancellationToken cancellationToken = default)
        => _contentCache.SetETagAsync(url, etag, cancellationToken);

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        => _contentCache.InvalidateAsync(key, cancellationToken);

    public async Task<LlmsDocument[]?> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _contentCache.GetJsonAsync(_indexCacheKey, JsonSourceGenerationContext.Default.LlmsDocumentArray, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (documents is not null)
        {
            await ClearLegacySourceCacheAsync(cancellationToken).ConfigureAwait(false);
        }

        return documents;
    }

    public async Task SetIndexAsync(LlmsDocument[] documents, CancellationToken cancellationToken = default)
    {
        await _contentCache.SetJsonAsync(_indexCacheKey, documents, JsonSourceGenerationContext.Default.LlmsDocumentArray, cancellationToken).ConfigureAwait(false);
        await ClearLegacySourceCacheAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> GetIndexSourceFingerprintAsync(CancellationToken cancellationToken = default)
        => _contentCache.GetAsync(_indexSourceFingerprintCacheKey, cancellationToken);

    public Task SetIndexSourceFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
        => _contentCache.SetAsync(_indexSourceFingerprintCacheKey, fingerprint, cancellationToken);

    private async Task ClearLegacySourceCacheAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(_sourceCacheKey, _llmsTxtUrl, StringComparison.Ordinal))
        {
            return;
        }

        await _contentCache.InvalidateAsync(_llmsTxtUrl, cancellationToken).ConfigureAwait(false);
        await _contentCache.SetETagAsync(_llmsTxtUrl, null, cancellationToken).ConfigureAwait(false);
    }
}
