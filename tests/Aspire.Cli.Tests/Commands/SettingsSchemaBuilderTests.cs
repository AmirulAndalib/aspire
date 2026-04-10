// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Commands;

namespace Aspire.Cli.Tests.Commands;

public class SettingsSchemaBuilderTests
{
    [Fact]
    public void BuildSchema_IncludesDocsSourceProperties()
    {
        var schema = SettingsSchemaBuilder.BuildSchema(excludeLocalOnly: false);

        var docsProperty = Assert.Single(schema.Properties, static property => property.Name == "docs");
        Assert.NotNull(docsProperty.SubProperties);

        var llmsProperty = Assert.Single(docsProperty.SubProperties, static property => property.Name == "llmsTxtUrl");
        Assert.Equal("string", llmsProperty.Type);

        var apiProperty = Assert.Single(docsProperty.SubProperties, static property => property.Name == "api");
        Assert.NotNull(apiProperty.SubProperties);

        var sitemapProperty = Assert.Single(apiProperty.SubProperties, static property => property.Name == "sitemapUrl");
        Assert.Equal("string", sitemapProperty.Type);
    }

    [Fact]
    public void BuildConfigFileSchema_IncludesDocsSourceProperties()
    {
        var schema = SettingsSchemaBuilder.BuildConfigFileSchema(excludeLocalOnly: false);

        var docsProperty = Assert.Single(schema.Properties, static property => property.Name == "docs");
        Assert.NotNull(docsProperty.SubProperties);

        var llmsProperty = Assert.Single(docsProperty.SubProperties, static property => property.Name == "llmsTxtUrl");
        Assert.Equal("string", llmsProperty.Type);

        var apiProperty = Assert.Single(docsProperty.SubProperties, static property => property.Name == "api");
        Assert.NotNull(apiProperty.SubProperties);

        var sitemapProperty = Assert.Single(apiProperty.SubProperties, static property => property.Name == "sitemapUrl");
        Assert.Equal("string", sitemapProperty.Type);
    }
}
