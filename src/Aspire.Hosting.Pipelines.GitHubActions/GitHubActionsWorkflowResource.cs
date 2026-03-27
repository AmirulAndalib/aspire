// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Pipelines.GitHubActions;

/// <summary>
/// Represents a GitHub Actions workflow as a pipeline environment resource.
/// </summary>
[Experimental("ASPIREPIPELINES001", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public class GitHubActionsWorkflowResource(string name) : Resource(name), IPipelineEnvironment
{
    private readonly List<GitHubActionsJob> _jobs = [];

    /// <summary>
    /// Gets the filename for the generated workflow YAML file (e.g., "deploy.yml").
    /// </summary>
    public string WorkflowFileName => $"{Name}.yml";

    /// <summary>
    /// Gets the jobs declared in this workflow.
    /// </summary>
    public IReadOnlyList<GitHubActionsJob> Jobs => _jobs;

    /// <summary>
    /// Adds a job to this workflow.
    /// </summary>
    /// <param name="id">The unique job identifier within the workflow.</param>
    /// <returns>The created <see cref="GitHubActionsJob"/>.</returns>
    public GitHubActionsJob AddJob(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_jobs.Any(j => j.Id == id))
        {
            throw new InvalidOperationException(
                $"A job with the ID '{id}' has already been added to the workflow '{Name}'.");
        }

        var job = new GitHubActionsJob(id, this);
        _jobs.Add(job);
        return job;
    }
}
