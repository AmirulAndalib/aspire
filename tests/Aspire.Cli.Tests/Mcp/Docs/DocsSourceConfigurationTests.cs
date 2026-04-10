// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Documentation.Docs;
using Microsoft.Extensions.Configuration;

namespace Aspire.Cli.Tests.Documentation.Docs;

public class DocsSourceConfigurationTests
{
    [Fact]
    public void GetLlmsTxtUrl_PrefersAspireConfigDocsPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["docs:llmsTxtUrl"] = "http://localhost:4321/llms-small.txt",
                [DocsSourceConfiguration.LlmsTxtUrlConfigKey] = "http://legacy.example/llms-small.txt"
            })
            .Build();

        var llmsTxtUrl = DocsSourceConfiguration.GetLlmsTxtUrl(configuration);

        Assert.Equal("http://localhost:4321/llms-small.txt", llmsTxtUrl);
    }

    [Fact]
    public void GetLlmsTxtUrl_FallsBackToLegacyConfigKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DocsSourceConfiguration.LlmsTxtUrlConfigKey] = "http://legacy.example/llms-small.txt"
            })
            .Build();

        var llmsTxtUrl = DocsSourceConfiguration.GetLlmsTxtUrl(configuration);

        Assert.Equal("http://legacy.example/llms-small.txt", llmsTxtUrl);
    }

    [Fact]
    public void GetContentCacheKey_DefaultSourceUsesFriendlyStem()
    {
        var cacheKey = DocsSourceConfiguration.GetContentCacheKey("https://aspire.dev/llms-small.txt");

        Assert.Equal("llms-small", cacheKey);
    }

    [Fact]
    public void GetIndexCacheKey_UsesSourceSpecificCacheKey()
    {
        var aspireDevKey = DocsSourceConfiguration.GetIndexCacheKey("https://aspire.dev/llms-small.txt");
        var localhostKey = DocsSourceConfiguration.GetIndexCacheKey("http://localhost:4321/llms-small.txt");

        Assert.Equal("index:llms-small", aspireDevKey);
        Assert.NotEqual(aspireDevKey, localhostKey);
    }
}
