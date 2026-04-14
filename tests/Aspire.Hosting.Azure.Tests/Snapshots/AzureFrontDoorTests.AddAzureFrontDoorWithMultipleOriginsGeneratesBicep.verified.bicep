@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param origin_0_host string

param origin_1_host string

resource frontdoor 'Microsoft.Cdn/profiles@2025-06-01' = {
  name: take('frontdoor${uniqueString(resourceGroup().id)}', 24)
  location: 'Global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
  tags: {
    'aspire-resource-name': 'frontdoor'
  }
}

resource frontDoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2025-06-01' = {
  name: take('frontdoorendpoint${uniqueString(resourceGroup().id)}', 24)
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
  parent: frontdoor
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2025-06-01' = {
  name: take('origingroup${uniqueString(resourceGroup().id)}', 24)
  properties: {
    healthProbeSettings: {
      probePath: '/'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 240
    }
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    sessionAffinityState: 'Disabled'
  }
  parent: frontdoor
}

resource origin0 'Microsoft.Cdn/profiles/originGroups/origins@2025-06-01' = {
  name: take('origin-0-${uniqueString(resourceGroup().id)}', 90)
  properties: {
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
    hostName: origin_0_host
    httpPort: 80
    httpsPort: 443
    originHostHeader: origin_0_host
    priority: 1
    weight: 1000
  }
  parent: originGroup
}

resource origin1 'Microsoft.Cdn/profiles/originGroups/origins@2025-06-01' = {
  name: take('origin-1-${uniqueString(resourceGroup().id)}', 90)
  properties: {
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
    hostName: origin_1_host
    httpPort: 80
    httpsPort: 443
    originHostHeader: origin_1_host
    priority: 1
    weight: 1000
  }
  parent: originGroup
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2025-06-01' = {
  name: take('route${uniqueString(resourceGroup().id)}', 24)
  properties: {
    cacheConfiguration: {
      queryStringCachingBehavior: 'IgnoreQueryString'
      compressionSettings: {
        contentTypesToCompress: [
          'text/plain'
          'text/html'
          'text/css'
          'application/javascript'
          'application/json'
          'image/svg+xml'
        ]
        isCompressionEnabled: true
      }
    }
    enabledState: 'Enabled'
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    originGroup: {
      id: originGroup.id
    }
    originPath: '/'
    patternsToMatch: [
      '/*'
    ]
    supportedProtocols: [
      'Http'
      'Https'
    ]
  }
  parent: frontDoorEndpoint
}

output endpointUrl string = 'https://${frontDoorEndpoint.properties.hostName}'