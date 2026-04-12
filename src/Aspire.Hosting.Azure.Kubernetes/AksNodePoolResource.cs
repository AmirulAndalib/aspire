// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure.Kubernetes;

/// <summary>
/// Represents an AKS node pool as a child resource of an <see cref="AzureKubernetesEnvironmentResource"/>.
/// Node pools can be referenced by compute resources to schedule workloads on specific node pools
/// using <see cref="AzureKubernetesEnvironmentExtensions.WithNodePoolAffinity{T}"/>.
/// </summary>
/// <param name="name">The name of the node pool resource.</param>
/// <param name="config">The node pool configuration.</param>
/// <param name="parent">The parent AKS environment resource.</param>
public class AksNodePoolResource(
    string name,
    AksNodePoolConfig config,
    AzureKubernetesEnvironmentResource parent) : Resource(name), IResourceWithParent<AzureKubernetesEnvironmentResource>
{
    /// <summary>
    /// Gets the parent AKS environment resource.
    /// </summary>
    public AzureKubernetesEnvironmentResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the node pool configuration.
    /// </summary>
    public AksNodePoolConfig Config { get; } = config ?? throw new ArgumentNullException(nameof(config));
}
