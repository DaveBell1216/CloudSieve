targetScope = 'resourceGroup'

@description('Azure region for the deployment.')
param location string = 'westus3'

@minLength(3)
@maxLength(15)
@description('Lowercase alphanumeric prefix used for globally unique resource names.')
param prefix string

@description('Object ID of the Microsoft Entra user or group that will administer Azure SQL.')
param sqlAdminObjectId string

@description('Display name of the Microsoft Entra SQL administrator.')
param sqlAdminName string

@allowed(['User', 'Group'])
@description('Principal type of the SQL administrator.')
param sqlAdminPrincipalType string = 'User'

var suffix = uniqueString(resourceGroup().id)
var registryName = '${prefix}${suffix}'
var sqlServerName = '${prefix}-${suffix}'
var sqlDatabaseName = 'primes'

resource vnet 'Microsoft.Network/virtualNetworks@2026-03-01' = {
  name: '${prefix}-vnet'
  location: location
  properties: {
	addressSpace: { addressPrefixes: ['10.90.0.0/16'] }
	subnets: [
	  {
		name: 'containerapps'
		properties: {
		  addressPrefix: '10.90.0.0/23'
		  delegations: [
			{
			  name: 'containerapps'
			  properties: { serviceName: 'Microsoft.App/environments' }
			}
		  ]
		}
	  }
	  {
		name: 'private-endpoints'
		properties: { addressPrefix: '10.90.2.0/24' }
	  }
	]
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2026-03-01' = {
  name: '${prefix}-logs-${suffix}'
  location: location
  properties: {
	sku: { name: 'PerGB2018' }
	retentionInDays: 30
  }
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
	appLogsConfiguration: {
	  destination: 'log-analytics'
	  logAnalyticsConfiguration: {
		customerId: workspace.properties.customerId
		sharedKey: workspace.listKeys().primarySharedKey
	  }
	}
	vnetConfiguration: {
	  infrastructureSubnetId: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'containerapps')
	  internal: true
	}
	workloadProfiles: [
	  { name: 'Consumption', workloadProfileType: 'Consumption' }
	]
  }
}

module environmentDns 'environment-dns.bicep' = {
  name: 'environment-dns'
  params: {
    domain: environment.properties.defaultDomain
    staticIp: environment.properties.staticIp
    vnetId: vnet.id
  }
}

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

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${prefix}-api-identity'
  location: location
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${prefix}-worker-identity'
  location: location
}

var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, apiIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
	roleDefinitionId: acrPullRoleId
	principalId: apiIdentity.properties.principalId
	principalType: 'ServicePrincipal'
  }
}
resource workerAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, workerIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
	roleDefinitionId: acrPullRoleId
	principalId: workerIdentity.properties.principalId
	principalType: 'ServicePrincipal'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' = {
  name: sqlServerName
  location: location
  properties: {
	version: '12.0'
	minimalTlsVersion: '1.2'
	publicNetworkAccess: 'Disabled'
	administrators: {
	  administratorType: 'ActiveDirectory'
	  principalType: sqlAdminPrincipalType
	  login: sqlAdminName
	  sid: sqlAdminObjectId
	  tenantId: tenant().tenantId
	  azureADOnlyAuthentication: true
	}
  }
}

resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { requestedBackupStorageRedundancy: 'Local' }
}

resource sqlDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink${az.environment().suffixes.sqlServerHostname}'
  location: 'global'
}
resource sqlDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: sqlDns
  name: 'vnet-link'
  location: 'global'
  properties: {
	registrationEnabled: false
	virtualNetwork: { id: vnet.id }
  }
}
resource sqlEndpoint 'Microsoft.Network/privateEndpoints@2026-03-01' = {
  name: '${prefix}-sql-private-endpoint'
  location: location
  properties: {
	subnet: { id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'private-endpoints') }
	privateLinkServiceConnections: [
	  {
		name: 'sql'
		properties: {
		  privateLinkServiceId: sqlServer.id
		  groupIds: ['sqlServer']
		}
	  }
	]
  }
}
resource sqlEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2026-03-01' = {
  parent: sqlEndpoint
  name: 'default'
  properties: {
	privateDnsZoneConfigs: [
	  { name: 'sql', properties: { privateDnsZoneId: sqlDns.id } }
	]
  }
}

output registryName string = registry.name
output registryServer string = registry.properties.loginServer
output environmentName string = environment.name
output sqlServerName string = sqlServer.name
output databaseName string = database.name
output apiIdentityName string = apiIdentity.name
output apiIdentityClientId string = apiIdentity.properties.clientId
output workerIdentityName string = workerIdentity.name
output workerIdentityPrincipalId string = workerIdentity.properties.principalId
output vnetName string = vnet.name
