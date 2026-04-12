// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure.Kubernetes;

/// <summary>
/// Annotation that associates a compute resource with a specific AKS node pool.
/// When present, the Kubernetes deployment will include a <c>nodeSelector</c> targeting
/// the specified node pool via the <c>agentpool</c> label.
/// </summary>
internal sealed class AksNodePoolAffinityAnnotation(AksNodePoolResource nodePool) : IResourceAnnotation
{
    /// <summary>
    /// Gets the node pool to schedule the workload on.
    /// </summary>
    public AksNodePoolResource NodePool { get; } = nodePool;
}
