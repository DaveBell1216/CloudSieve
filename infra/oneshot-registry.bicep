targetScope = 'resourceGroup'

@description('Azure region for the temporary registry.')
param location string = 'westus3'

@minLength(3)
@maxLength(15)
@description('Lowercase alphanumeric prefix for globally unique names.')
param prefix string

var registryName = '${prefix}${uniqueString(resourceGroup().id)}'

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  properties: {
	adminUserEnabled: false
	anonymousPullEnabled: false
	policies: { azureADAuthenticationAsArmPolicy: { status: 'enabled' } }
  }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${prefix}-oneshot-pull'
  location: location
}

var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
	roleDefinitionId: acrPullRoleId
	principalId: pullIdentity.properties.principalId
	principalType: 'ServicePrincipal'
  }
}

output registryName string = registry.name
output registryServer string = registry.properties.loginServer
output pullIdentityId string = pullIdentity.id
