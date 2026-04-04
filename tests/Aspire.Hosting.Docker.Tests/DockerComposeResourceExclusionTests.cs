// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES003

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Docker.Tests;

public class DockerComposeResourceExclusionTests(ITestOutputHelper output)
{
    /// <summary>
    /// A resource that is excluded from publish (via ExcludeFromManifest) but still
    /// referenced by another resource. The publisher should handle this gracefully
    /// instead of crashing with a KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task ReferencedResourceExcludedFromPublish_ShouldNotCrash()
    {
        using var tempDir = new TestTempDirectory();
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish, tempDir.Path);
        builder.Services.AddSingleton<IResourceContainerImageManager, MockImageBuilder>();
        builder.AddDockerComposeEnvironment("compose");

        var excluded = builder.AddContainer("excluded", "myimage")
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .ExcludeFromManifest();

        builder.AddContainer("consumer", "consumerimage")
            .WithReference(excluded.GetEndpoint("http"));

        var app = builder.Build();
        app.Run();

        var composePath = Path.Combine(tempDir.Path, "docker-compose.yaml");
        Assert.True(File.Exists(composePath), "docker-compose.yaml should be generated");

        var content = File.ReadAllText(composePath);
        output.WriteLine(content);

        // The consumer should be in the compose file
        Assert.Contains("consumer", content);
    }

    /// <summary>
    /// A resource that targets a different compute environment but is referenced by a
    /// resource in the current environment. The publisher should handle this gracefully.
    /// </summary>
    [Fact]
    public async Task ReferencedResourceTargetingDifferentEnvironment_ShouldNotCrash()
    {
        using var tempDir = new TestTempDirectory();
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish, tempDir.Path);
        builder.Services.AddSingleton<IResourceContainerImageManager, MockImageBuilder>();

        var compose1 = builder.AddDockerComposeEnvironment("compose1");
        var compose2 = builder.AddDockerComposeEnvironment("compose2");

        var otherEnv = builder.AddContainer("other-env-service", "otherimage")
            .WithHttpEndpoint(name: "http", targetPort: 9090)
            .WithComputeEnvironment(compose2);

        builder.AddContainer("consumer", "consumerimage")
            .WithComputeEnvironment(compose1)
            .WithReference(otherEnv.GetEndpoint("http"));

        var app = builder.Build();
        app.Run();

        var composePath = Path.Combine(tempDir.Path, "docker-compose.yaml");
        Assert.True(File.Exists(composePath), "docker-compose.yaml should be generated");

        var content = File.ReadAllText(composePath);
        output.WriteLine(content);

        // The consumer should be in compose1
        Assert.Contains("consumer", content);
    }

    /// <summary>
    /// A build-only container (like a JS app without PublishAs*) that is referenced by
    /// another resource. The publisher should handle this gracefully — either by
    /// including the resource in the mapping or by producing a clear error.
    /// Currently this crashes with KeyNotFoundException in the pipeline.
    /// </summary>
    [Fact]
    public async Task ReferencedBuildOnlyContainer_ShouldNotCrash()
    {
        using var tempDir = new TestTempDirectory();
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish, tempDir.Path);
        builder.Services.AddSingleton<IResourceContainerImageManager, MockImageBuilder>();
        builder.AddDockerComposeEnvironment("compose");

        // Simulate a build-only JS app: PublishAsDockerFile but HasEntrypoint = false
        var buildOnly = builder.AddExecutable("frontend", "npm", ".", "run", "dev")
            .WithHttpEndpoint(name: "http", targetPort: 3000)
            .PublishAsDockerFile(c =>
            {
                if (c.Resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var ann))
                {
                    ann.HasEntrypoint = false;
                }
            });

        builder.AddContainer("api", "apiimage")
            .WithReference(buildOnly.GetEndpoint("http"));

        var app = builder.Build();
        app.Run();

        // The compose file should exist and contain the api service.
        // The pipeline currently logs a KeyNotFoundException but doesn't throw.
        var composePath = Path.Combine(tempDir.Path, "docker-compose.yaml");
        Assert.True(File.Exists(composePath), "docker-compose.yaml should be generated");

        var content = File.ReadAllText(composePath);
        output.WriteLine(content);

        // The api service should be in the compose file with a reference to frontend
        Assert.Contains("api", content);
    }

    /// <summary>
    /// A build-only container that is NOT referenced by anyone via PublishWithContainerFiles
    /// should produce a clear error — the user likely forgot to call a PublishAs* method.
    /// This is the "silent omission" pit of failure.
    /// </summary>
    [Fact]
    public async Task UnreferencedBuildOnlyContainer_ShouldFailWithClearError()
    {
        using var tempDir = new TestTempDirectory();
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish, tempDir.Path);
        builder.Services.AddSingleton<IResourceContainerImageManager, MockImageBuilder>();
        builder.AddDockerComposeEnvironment("compose");

        builder.AddExecutable("frontend", "npm", ".", "run", "dev")
            .PublishAsDockerFile(c =>
            {
                if (c.Resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var ann))
                {
                    ann.HasEntrypoint = false;
                }
            });

        builder.AddContainer("api", "apiimage")
            .WithHttpEndpoint(name: "http", targetPort: 3000);

        var app = builder.Build();
        app.Run();

        // The pipeline should report an error about the orphaned build-only resource
        var composePath = Path.Combine(tempDir.Path, "docker-compose.yaml");
        var content = File.Exists(composePath) ? File.ReadAllText(composePath) : "";
        output.WriteLine(content);

        // frontend should NOT silently disappear — it should produce an error
        // For now, assert that it's missing (the current broken behavior)
        // After the fix, this test should assert that the pipeline fails with a clear message
        Assert.DoesNotContain("frontend", content);
    }

    private sealed class MockImageBuilder : IResourceContainerImageManager
    {
        public Task BuildImageAsync(IResource resource, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BuildImagesAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PushImageAsync(IResource resource, CancellationToken cancellationToken)
            => Task.CompletedTask;

#pragma warning disable CA1822, IDE0060
        public Task PushImagesAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
#pragma warning restore CA1822, IDE0060
    }
}
