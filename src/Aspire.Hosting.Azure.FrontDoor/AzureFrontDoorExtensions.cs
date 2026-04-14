// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable ASPIREAZURE003 // Type is for evaluation purposes only and is subject to change or removal in future updates.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Core;
using Azure.Provisioning;
using Azure.Provisioning.Cdn;
using Azure.Provisioning.Expressions;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Azure Front Door resources to an Aspire application.
/// </summary>
public static class AzureFrontDoorExtensions
{
    /// <summary>
    /// Adds an Azure Front Door resource to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Azure Front Door is a global, scalable entry point that uses the Microsoft global edge network to create
    /// fast, secure, and widely scalable web applications. Use <see cref="WithOrigin"/> to add origins
    /// (backends) to the Front Door resource.
    /// </para>
    /// <example>
    /// Add an Azure Front Door resource with an App Service origin:
    /// <code>
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api");
    /// var frontDoor = builder.AddAzureFrontDoor("frontdoor")
    ///     .WithOrigin(api.GetEndpoint("https"));
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport(Description = "Adds an Azure Front Door resource")]
    public static IResourceBuilder<AzureFrontDoorResource> AddAzureFrontDoor(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddAzureProvisioning();

        var configureInfrastructure = static (AzureResourceInfrastructure infrastructure) =>
        {
            var azureResource = (AzureFrontDoorResource)infrastructure.AspireResource;

            // Create the CDN profile (Front Door)
            var profile = new CdnProfile(infrastructure.AspireResource.GetBicepIdentifier())
            {
                SkuName = CdnSkuName.StandardAzureFrontDoor,
                Location = new AzureLocation("Global"),
                Tags = { { "aspire-resource-name", infrastructure.AspireResource.Name } }
            };
            infrastructure.Add(profile);

            // Create the Front Door endpoint
            var endpoint = new FrontDoorEndpoint("frontDoorEndpoint")
            {
                Parent = profile,
                Location = new AzureLocation("Global"),
                EnabledState = EnabledState.Enabled
            };
            infrastructure.Add(endpoint);

            // Create the origin group with default health probe and load balancing settings
            var originGroup = new FrontDoorOriginGroup("originGroup")
            {
                Parent = profile,
                HealthProbeSettings = new HealthProbeSettings
                {
                    ProbePath = "/",
                    ProbeRequestType = HealthProbeRequestType.Head,
                    ProbeProtocol = HealthProbeProtocol.Https,
                    ProbeIntervalInSeconds = 240
                },
                LoadBalancingSettings = new LoadBalancingSettings
                {
                    SampleSize = 4,
                    SuccessfulSamplesRequired = 3,
                    AdditionalLatencyInMilliseconds = 50
                },
                SessionAffinityState = EnabledState.Disabled
            };
            infrastructure.Add(originGroup);

            // Create origins from annotations
            var origins = azureResource.Annotations.OfType<AzureFrontDoorOriginAnnotation>().ToList();
            for (var i = 0; i < origins.Count; i++)
            {
                var originAnnotation = origins[i];
                // Use the Host property (not the full URL) since Front Door hostName requires just the hostname
                var hostExpression = ReferenceExpression.Create($"{originAnnotation.Endpoint.Property(EndpointProperty.Host)}");
                var hostParam = hostExpression.AsProvisioningParameter(infrastructure, $"origin_{i}_host");

                var origin = new FrontDoorOrigin($"origin{i}")
                {
                    Parent = originGroup,
                    Name = BicepFunction.Take(BicepFunction.Interpolate($"origin-{i}-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"), 90),
                    HostName = hostParam,
                    OriginHostHeader = hostParam,
                    HttpPort = 80,
                    HttpsPort = 443,
                    Priority = 1,
                    Weight = 1000,
                    EnabledState = EnabledState.Enabled,
                    EnforceCertificateNameCheck = true
                };
                infrastructure.Add(origin);
            }

            // Create the default route
            var route = new FrontDoorRoute("route")
            {
                Parent = endpoint,
                OriginGroupId = originGroup.Id,
                PatternsToMatch = ["/*"],
                ForwardingProtocol = ForwardingProtocol.HttpsOnly,
                LinkToDefaultDomain = LinkToDefaultDomain.Enabled,
                HttpsRedirect = HttpsRedirect.Enabled,
                EnabledState = EnabledState.Enabled,
                OriginPath = "/",
                SupportedProtocols = [FrontDoorEndpointProtocol.Http, FrontDoorEndpointProtocol.Https],
                CacheConfiguration = new FrontDoorRouteCacheConfiguration
                {
                    QueryStringCachingBehavior = FrontDoorQueryStringCachingBehavior.IgnoreQueryString,
                    CompressionSettings = new RouteCacheCompressionSettings
                    {
                        IsCompressionEnabled = true,
                        ContentTypesToCompress =
                        [
                            "text/plain",
                            "text/html",
                            "text/css",
                            "application/javascript",
                            "application/json",
                            "image/svg+xml"
                        ]
                    }
                }
            };
            infrastructure.Add(route);

            // Output the Front Door endpoint URL
            infrastructure.Add(new ProvisioningOutput("endpointUrl", typeof(string))
            {
                Value = BicepFunction.Interpolate($"https://{endpoint.HostName}")
            });
        };

        var resource = new AzureFrontDoorResource(name, configureInfrastructure);

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds an origin (backend) to the Azure Front Door resource using an endpoint reference.
    /// </summary>
    /// <param name="builder">The Azure Front Door resource builder.</param>
    /// <param name="endpoint">The endpoint reference of the origin resource (e.g., from an App Service or Container App).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    /// <remarks>
    /// Each call to <see cref="WithOrigin"/> adds another origin to the Front Door's origin group.
    /// Multiple origins enable load balancing across backends.
    /// <example>
    /// Add multiple origins:
    /// <code>
    /// var frontDoor = builder.AddAzureFrontDoor("frontdoor")
    ///     .WithOrigin(api.GetEndpoint("https"))
    ///     .WithOrigin(web.GetEndpoint("https"));
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport(Description = "Adds an origin (backend endpoint) to the Azure Front Door resource")]
    public static IResourceBuilder<AzureFrontDoorResource> WithOrigin(
        this IResourceBuilder<AzureFrontDoorResource> builder,
        EndpointReference endpoint)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);

        return builder.WithAnnotation(new AzureFrontDoorOriginAnnotation(endpoint));
    }
}
