// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Utils;
using static Aspire.Hosting.Utils.AzureManifestUtils;

namespace Aspire.Hosting.Azure.Tests;

public class AzureFrontDoorTests
{
    [Fact]
    public void AddAzureFrontDoorCreatesResource()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var frontDoor = builder.AddAzureFrontDoor("frontdoor");

        Assert.NotNull(frontDoor);
        Assert.Equal("frontdoor", frontDoor.Resource.Name);
        Assert.IsType<AzureFrontDoorResource>(frontDoor.Resource);
    }

    [Fact]
    public void WithOriginAddsAnnotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var api = builder.AddProject<Project>("api", launchProfileName: null)
            .WithHttpsEndpoint();

        var frontDoor = builder.AddAzureFrontDoor("frontdoor")
            .WithOrigin(api.GetEndpoint("https"));

        var annotations = frontDoor.Resource.Annotations.OfType<AzureFrontDoorOriginAnnotation>().ToList();
        Assert.Single(annotations);
    }

    [Fact]
    public void WithOriginSupportsMultipleOrigins()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var api = builder.AddProject<Project>("api", launchProfileName: null)
            .WithHttpsEndpoint();
        var web = builder.AddProject<Project>("web", launchProfileName: null)
            .WithHttpsEndpoint();

        var frontDoor = builder.AddAzureFrontDoor("frontdoor")
            .WithOrigin(api.GetEndpoint("https"))
            .WithOrigin(web.GetEndpoint("https"));

        var annotations = frontDoor.Resource.Annotations.OfType<AzureFrontDoorOriginAnnotation>().ToList();
        Assert.Equal(2, annotations.Count);
    }

    [Fact]
    public async Task AddAzureFrontDoorGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var api = builder.AddProject<Project>("api", launchProfileName: null)
            .WithHttpsEndpoint();

        var frontDoor = builder.AddAzureFrontDoor("frontdoor")
            .WithOrigin(api.GetEndpoint("https"));

        var (_, bicep) = await GetManifestWithBicep(frontDoor.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public async Task AddAzureFrontDoorWithMultipleOriginsGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var api = builder.AddProject<Project>("api", launchProfileName: null)
            .WithHttpsEndpoint();
        var web = builder.AddProject<Project>("web", launchProfileName: null)
            .WithHttpsEndpoint();

        var frontDoor = builder.AddAzureFrontDoor("frontdoor")
            .WithOrigin(api.GetEndpoint("https"))
            .WithOrigin(web.GetEndpoint("https"));

        var (_, bicep) = await GetManifestWithBicep(frontDoor.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public void EndpointUrlOutputReferenceIsAvailable()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var frontDoor = builder.AddAzureFrontDoor("frontdoor");

        var endpointUrl = frontDoor.Resource.EndpointUrl;
        Assert.NotNull(endpointUrl);
        Assert.Equal("endpointUrl", endpointUrl.Name);
    }

    [Fact]
    public void AddAzureFrontDoorThrowsOnNullName()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        Assert.Throws<ArgumentNullException>(() => builder.AddAzureFrontDoor(null!));
    }

    [Fact]
    public void WithOriginThrowsOnNullEndpoint()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var frontDoor = builder.AddAzureFrontDoor("frontdoor");

        Assert.Throws<ArgumentNullException>(() => frontDoor.WithOrigin(null!));
    }

    private sealed class Project : IProjectMetadata
    {
        public string ProjectPath => "project";
    }
}
