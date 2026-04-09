// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Aspire.Cli.Documentation.Docs;
using Aspire.Cli.Tests.Utils;
using Microsoft.AspNetCore.InternalTesting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Cli.Tests.Documentation.Docs;

public class DocsCacheTests(ITestOutputHelper outputHelper)
{
    private const string DefaultLlmsTxtUrl = "https://aspire.dev/llms-small.txt";

    [Fact]
    public async Task FetchDocsAsync_PersistsFriendlyLlmsCacheFileName()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder().Build();
        var cache = CreateCache(workspace, memoryCache);

        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("# Content")
        };

        using var handler = new MockHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var fetcher = new DocsFetcher(httpClient, cache, configuration, NullLogger<DocsFetcher>.Instance);

        _ = await fetcher.FetchDocsAsync().DefaultTimeout();

        var cacheFiles = Directory.GetFiles(Path.Combine(workspace.WorkspaceRoot.FullName, ".aspire", "cache", "docs"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Contains("llms-small.txt", cacheFiles);
        Assert.DoesNotContain("https___aspire.dev_llms-small.txt.txt", cacheFiles);
    }

    [Fact]
    public async Task GetIndexAsync_ClearsLegacyUrlBackedDocsFiles_WhenModernIndexExists()
    {
        using var workspace = TemporaryWorkspace.Create(outputHelper);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(workspace, memoryCache);

        await cache.SetAsync(DefaultLlmsTxtUrl, "# Legacy Content").DefaultTimeout();
        await cache.SetETagAsync(DefaultLlmsTxtUrl, "\"legacy-etag\"").DefaultTimeout();
        await cache.SetIndexAsync(
            [
                new LlmsDocument
                {
                    Title = "Document",
                    Slug = "document",
                    Content = "# Document",
                    Sections = [],
                    Summary = "Summary"
                }
            ]).DefaultTimeout();

        _ = await cache.GetIndexAsync().DefaultTimeout();

        var cacheFiles = Directory.GetFiles(Path.Combine(workspace.WorkspaceRoot.FullName, ".aspire", "cache", "docs"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.DoesNotContain("https___aspire.dev_llms-small.txt.txt", cacheFiles);
        Assert.DoesNotContain("https___aspire.dev_llms-small.txt.etag.txt", cacheFiles);
        Assert.Contains("index_llms-small.json", cacheFiles);
    }

    private static DocsCache CreateCache(TemporaryWorkspace workspace, IMemoryCache memoryCache)
    {
        var configuration = new ConfigurationBuilder().Build();
        var executionContext = new CliExecutionContext(
            workspace.WorkspaceRoot,
            new DirectoryInfo(Path.Combine(workspace.WorkspaceRoot.FullName, ".aspire", "hives")),
            new DirectoryInfo(Path.Combine(workspace.WorkspaceRoot.FullName, ".aspire", "cache")),
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aspire-test-runtimes")),
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aspire-test-logs")),
            "test.log");

        return new DocsCache(memoryCache, executionContext, configuration, NullLogger<DocsCache>.Instance);
    }
}
