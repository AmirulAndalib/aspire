@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

output id string = id

output name string = name

output clusterFqdn string = clusterFqdn

output oidcIssuerUrl string = oidcIssuerUrl

output kubeletIdentityObjectId string = kubeletIdentityObjectId

output nodeResourceGroup string = nodeResourceGroup