@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param privatelink_openai_azure_com_outputs_name string

param myvnet_outputs_pesubnet_id string

param openai_outputs_id string

resource privatelink_openai_azure_com 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: privatelink_openai_azure_com_outputs_name
}

resource pesubnet_openai_pe 'Microsoft.Network/privateEndpoints@2025-05-01' = {
  name: take('pesubnet_openai_pe-${uniqueString(resourceGroup().id)}', 64)
  location: location
  properties: {
    privateLinkServiceConnections: [
      {
        properties: {
          privateLinkServiceId: openai_outputs_id
          groupIds: [
            'account'
          ]
        }
        name: 'pesubnet-openai-pe-connection'
      }
    ]
    subnet: {
      id: myvnet_outputs_pesubnet_id
    }
  }
  tags: {
    'aspire-resource-name': 'pesubnet-openai-pe'
  }
}

resource pesubnet_openai_pe_dnsgroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2025-05-01' = {
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink_openai_azure_com'
        properties: {
          privateDnsZoneId: privatelink_openai_azure_com.id
        }
      }
    ]
  }
  parent: pesubnet_openai_pe
}

output id string = pesubnet_openai_pe.id

output name string = pesubnet_openai_pe.name