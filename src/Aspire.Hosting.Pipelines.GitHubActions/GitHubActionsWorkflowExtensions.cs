// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Pipelines.GitHubActions;

/// <summary>
/// Extension methods for adding GitHub Actions workflow resources to a distributed application.
/// </summary>
[Experimental("ASPIREPIPELINES001", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public static class GitHubActionsWorkflowExtensions
{
    /// <summary>
    /// Adds a GitHub Actions workflow resource to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the workflow resource. This also becomes the workflow filename (e.g., "deploy" → "deploy.yml").</param>
    /// <returns>A resource builder for the workflow resource.</returns>
    [AspireExportIgnore(Reason = "Pipeline generation is not yet ATS-compatible")]
    public static IResourceBuilder<GitHubActionsWorkflowResource> AddGitHubActionsWorkflow(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = new GitHubActionsWorkflowResource(name);

        resource.Annotations.Add(new PipelineEnvironmentCheckAnnotation(context =>
        {
            // This environment is relevant when running inside GitHub Actions
            var isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            return Task.FromResult(isGitHubActions);
        }));

        return builder.AddResource(resource)
            .ExcludeFromManifest();
    }
}
