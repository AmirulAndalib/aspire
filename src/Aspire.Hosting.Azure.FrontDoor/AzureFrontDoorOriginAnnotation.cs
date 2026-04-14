// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Represents an origin to be added to an Azure Front Door resource.
/// </summary>
internal sealed class AzureFrontDoorOriginAnnotation(EndpointReference endpoint) : IResourceAnnotation
{
    /// <summary>
    /// Gets the endpoint reference for this origin.
    /// </summary>
    public EndpointReference Endpoint { get; } = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
}
