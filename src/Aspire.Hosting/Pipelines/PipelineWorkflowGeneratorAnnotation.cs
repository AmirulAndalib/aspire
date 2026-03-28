// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Pipelines;

/// <summary>
/// An annotation that provides a callback to generate workflow files for a pipeline environment.
/// </summary>
/// <remarks>
/// Pipeline environment resources (e.g., GitHub Actions workflows) annotate themselves with this
/// to provide the implementation for generating CI/CD workflow files during <c>aspire pipeline init</c>.
/// </remarks>
[Experimental("ASPIREPIPELINES001", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public sealed class PipelineWorkflowGeneratorAnnotation(Func<PipelineWorkflowGenerationContext, Task> generateAsync) : IResourceAnnotation
{
    /// <summary>
    /// Generates the workflow files for the pipeline environment.
    /// </summary>
    /// <param name="context">The generation context.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task GenerateAsync(PipelineWorkflowGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return generateAsync(context);
    }
}

/// <summary>
/// Context provided to pipeline workflow generators.
/// </summary>
[Experimental("ASPIREPIPELINES001", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public sealed class PipelineWorkflowGenerationContext
{
    /// <summary>
    /// Gets the pipeline step context for the current execution.
    /// </summary>
    public required PipelineStepContext StepContext { get; init; }

    /// <summary>
    /// Gets the pipeline environment resource that the workflow is being generated for.
    /// </summary>
    public required IPipelineEnvironment Environment { get; init; }

    /// <summary>
    /// Gets the pipeline steps registered in the app model.
    /// </summary>
    public required IReadOnlyList<PipelineStep> Steps { get; init; }

    /// <summary>
    /// Gets or sets the output directory for generated files. Defaults to the current directory.
    /// </summary>
    public string OutputDirectory { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken => StepContext.CancellationToken;
}
