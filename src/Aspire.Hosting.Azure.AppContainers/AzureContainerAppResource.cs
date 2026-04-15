// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREAZURE001
#pragma warning disable ASPIREPIPELINES003

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Azure.AppContainers;

/// <summary>
/// Represents an Azure Container App resource.
/// </summary>
public class AzureContainerAppResource : AzureProvisioningResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureContainerAppResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource in the Aspire application model.</param>
    /// <param name="configureInfrastructure">Callback to configure the Azure resources.</param>
    /// <param name="targetResource">The target compute resource that this Azure Container App is being created for.</param>
    public AzureContainerAppResource(string name, Action<AzureResourceInfrastructure> configureInfrastructure, IResource targetResource)
        : base(name, configureInfrastructure)
    {
        TargetResource = targetResource;

        // Add pipeline step to allocate endpoint values after provisioning
        // This is needed for cross-environment references: when another resource (e.g., on App Service)
        // references an endpoint on this container app, its SetParametersAsync needs to resolve the
        // endpoint URL. Without allocated endpoints, GetValueAsync() blocks forever.
        Annotations.Add(new PipelineStepAnnotation((factoryContext) =>
        {
            var dta = targetResource.GetDeploymentTargetAnnotation();
            if (dta is null)
            {
                return [];
            }

            var allocateEndpointsStep = new PipelineStep
            {
                Name = $"allocate-endpoints-{targetResource.Name}",
                Description = $"Allocates endpoint values for {targetResource.Name} after provisioning.",
                Action = async ctx =>
                {
                    var containerAppEnv = (AzureContainerAppEnvironmentResource)dta.ComputeEnvironment!;
                    var domainValue = await containerAppEnv.ContainerAppDomain.GetValueAsync(ctx.CancellationToken).ConfigureAwait(false);

                    if (targetResource.TryGetEndpoints(out var endpoints))
                    {
                        foreach (var endpoint in endpoints)
                        {
                            var fqdn = endpoint.IsExternal
                                ? $"{targetResource.Name.ToLowerInvariant()}.{domainValue}"
                                : $"{targetResource.Name.ToLowerInvariant()}.internal.{domainValue}";

                            var port = string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

                            var allocatedEndpoint = new AllocatedEndpoint(
                                endpoint, fqdn, port);

                            endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
                                KnownNetworkIdentifiers.LocalhostNetwork, allocatedEndpoint);
                        }
                    }
                },
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure]
            };
            allocateEndpointsStep.DependsOn($"provision-{name}");
            allocateEndpointsStep.RequiredBy(AzureEnvironmentResource.ProvisionInfrastructureStepName);

            return [allocateEndpointsStep];
        }));

        // Add pipeline step annotation for deploy
        Annotations.Add(new PipelineStepAnnotation((factoryContext) =>
        {
            // Get the deployment target annotation
            var deploymentTargetAnnotation = targetResource.GetDeploymentTargetAnnotation();
            if (deploymentTargetAnnotation is null)
            {
                return [];
            }

            var steps = new List<PipelineStep>();

            var printResourceSummary = new PipelineStep
            {
                Name = $"print-{targetResource.Name}-summary",
                Description = $"Prints the deployment summary and URL for {targetResource.Name}.",
                Action = async ctx =>
                {
                    var containerAppEnv = (AzureContainerAppEnvironmentResource)deploymentTargetAnnotation.ComputeEnvironment!;

                    var domainValue = await containerAppEnv.ContainerAppDomain.GetValueAsync(ctx.CancellationToken).ConfigureAwait(false);

                    if (targetResource.TryGetEndpoints(out var endpoints) && endpoints.Any(e => e.IsExternal))
                    {
                        var endpoint = $"https://{targetResource.Name.ToLowerInvariant()}.{domainValue}";

                        ctx.ReportingStep.Log(LogLevel.Information, new MarkdownString($"Successfully deployed **{targetResource.Name}** to [{endpoint}]({endpoint})"));
                        ctx.Summary.Add(targetResource.Name, new MarkdownString($"[{endpoint}]({endpoint})"));
                    }
                    else
                    {
                        ctx.ReportingStep.Log(LogLevel.Information, new MarkdownString($"Successfully deployed **{targetResource.Name}** to Azure Container Apps environment **{containerAppEnv.Name}**. No public endpoints were configured."));
                        ctx.Summary.Add(targetResource.Name, "No public endpoints");
                    }
                },
                Tags = ["print-summary"],
                RequiredBySteps = [WellKnownPipelineSteps.Deploy]
            };

            var deployStep = new PipelineStep
            {
                Name = $"deploy-{targetResource.Name}",
                Description = $"Aggregation step for deploying {targetResource.Name} to Azure Container Apps.",
                Action = _ => Task.CompletedTask,
                Tags = [WellKnownPipelineTags.DeployCompute]
            };

            deployStep.DependsOn(printResourceSummary);

            steps.Add(printResourceSummary);
            steps.Add(deployStep);

            return steps;
        }));

        // Add pipeline configuration annotation to wire up dependencies
        Annotations.Add(new PipelineConfigurationAnnotation((context) =>
        {
            var provisionSteps = context.GetSteps(this, WellKnownPipelineTags.ProvisionInfrastructure);

            // The app deployment should depend on push steps from the target resource
            var pushSteps = context.GetSteps(targetResource, WellKnownPipelineTags.PushContainerImage);
            provisionSteps.DependsOn(pushSteps);

            // Ensure summary step runs after provision
            context.GetSteps(this, "print-summary").DependsOn(provisionSteps);
        }));
    }

    /// <summary>
    /// Gets the target resource that this Azure Container App is being created for.
    /// </summary>
    public IResource TargetResource { get; }
}
