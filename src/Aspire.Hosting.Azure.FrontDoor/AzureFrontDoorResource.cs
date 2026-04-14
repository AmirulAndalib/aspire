// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Azure;

/// <summary>
/// Represents an Azure Front Door resource in the distributed application model.
/// </summary>
/// <remarks>
/// Azure Front Door is a global, scalable entry point that uses the Microsoft global edge network to create
/// fast, secure, and widely scalable web applications. It provides load balancing, SSL offloading,
/// and application acceleration for your web applications.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="configureInfrastructure">Callback to configure the Azure resources.</param>
public class AzureFrontDoorResource(string name, Action<AzureResourceInfrastructure> configureInfrastructure)
    : AzureProvisioningResource(name, configureInfrastructure)
{
    /// <summary>
    /// Gets the "endpointUrl" output reference from the bicep template for the Azure Front Door resource.
    /// </summary>
    /// <remarks>
    /// This is the URL of the Front Door endpoint (e.g., <c>https://&lt;endpoint-name&gt;.azurefd.net</c>).
    /// </remarks>
    public BicepOutputReference EndpointUrl => new("endpointUrl", this);
}
