// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Documentation.ApiDocs;

namespace Aspire.Cli.Tests.Documentation.ApiDocs;

public class ApiMemberMarkdownParserTests
{
    [Fact]
    public void Parse_UsesKnownMemberGroupRoutesWithoutLanguageSpecificChecks()
    {
        var containerItem = new ApiReferenceItem
        {
            Id = "python/aspire.python.test/testresource",
            Name = "TestResource",
            Language = ApiReferenceLanguages.Python,
            Kind = ApiReferenceKinds.Symbol,
            PageUrl = "https://aspire.dev/reference/api/python/aspire.python.test/testresource"
        };

        var memberGroupItem = new ApiReferenceItem
        {
            Id = "python/aspire.python.test/testresource/methods",
            Name = "methods",
            Language = ApiReferenceLanguages.Python,
            Kind = ApiReferenceKinds.MemberGroup,
            PageUrl = "https://aspire.dev/reference/api/python/aspire.python.test/testresource/methods",
            ParentId = containerItem.Id,
            MemberGroup = "methods"
        };

        var items = ApiMemberMarkdownParser.Parse(
            containerItem,
            """
            # TestResource

            ## Methods

            - [RunEmulator()](/reference/api/python/aspire.python.test/testresource/methods.md#runemulator) : `None` -- Runs the emulator.
            - [OtherType](/reference/api/python/aspire.python.test/othertype.md) -- Should not be treated as a member.
            """,
            ApiDocsSourceConfiguration.DefaultSitemapUrl,
            new Dictionary<string, ApiReferenceItem>(StringComparer.OrdinalIgnoreCase)
            {
                [memberGroupItem.PageUrl] = memberGroupItem
            });

        var item = Assert.Single(items);
        Assert.Equal("python/aspire.python.test/testresource/methods#runemulator", item.Id);
        Assert.Equal("RunEmulator()", item.Name);
        Assert.Equal(ApiReferenceLanguages.Python, item.Language);
        Assert.Equal(ApiReferenceKinds.Member, item.Kind);
        Assert.Equal(memberGroupItem.Id, item.ParentId);
        Assert.Equal("Runs the emulator.", item.Summary);
    }

    [Fact]
    public void Parse_ShortenOverloadIdsWhileKeepingThemDistinct()
    {
        var containerItem = new ApiReferenceItem
        {
            Id = "csharp/aspire.test.package/resourcebuilderextensions",
            Name = "ResourceBuilderExtensions",
            Language = ApiReferenceLanguages.CSharp,
            Kind = ApiReferenceKinds.Type,
            PageUrl = "https://aspire.dev/reference/api/csharp/aspire.test.package/resourcebuilderextensions"
        };

        var memberGroupItem = new ApiReferenceItem
        {
            Id = "csharp/aspire.test.package/resourcebuilderextensions/methods",
            Name = "methods",
            Language = ApiReferenceLanguages.CSharp,
            Kind = ApiReferenceKinds.MemberGroup,
            PageUrl = "https://aspire.dev/reference/api/csharp/aspire.test.package/resourcebuilderextensions/methods",
            ParentId = containerItem.Id,
            MemberGroup = "methods"
        };

        var items = ApiMemberMarkdownParser.Parse(
            containerItem,
            """
            # ResourceBuilderExtensions

            ## Methods

            - [WithEnvironment(IResourceBuilder<T>, string, string?)](/reference/api/csharp/aspire.test.package/resourcebuilderextensions/methods.md#withenvironment-iresourcebuilder-t-string-string) : `IResourceBuilder<T>` -- Adds a string environment variable.
            - [WithEnvironment(IResourceBuilder<T>, Action<EnvironmentCallbackContext>)](/reference/api/csharp/aspire.test.package/resourcebuilderextensions/methods.md#withenvironment-iresourcebuilder-t-action-environmentcallbackcontext) : `IResourceBuilder<T>` -- Adds environment variables from a callback.
            """,
            ApiDocsSourceConfiguration.DefaultSitemapUrl,
            new Dictionary<string, ApiReferenceItem>(StringComparer.OrdinalIgnoreCase)
            {
                [memberGroupItem.PageUrl] = memberGroupItem
            });

        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.True(item.Id.StartsWith("csharp/aspire.test.package/resourcebuilderextensions/methods#withenvironment-", StringComparison.Ordinal), item.Id);
            Assert.DoesNotContain("iresourcebuilder", item.Id, StringComparison.OrdinalIgnoreCase);
        });
        Assert.NotEqual(items[0].Id, items[1].Id);
    }
}
