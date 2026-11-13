targetScope = 'resourceGroup'

@description('Name prefix used in the foundation deployment.')
param prefix string

@description('Azure region used for the foundation deployment.')
param location string = 'westus3'

@description('Entra tenant ID that issued the API tokens.')
param tenantId string = tenant().tenantId

@description('Client ID of the API app registration, configured to issue v2 access tokens.')
param apiClientId string

@description('Tag of the images already pushed to the foundation registry.')
param imageTag string

@minValue(1)
@maxValue(32)
@description('Maximum number of worker replicas in a manually started execution.')
param workerParallelism int = 1

var suffix = uniqueString(resourceGroup().id)
var registryName = '${prefix}${suffix}'
var sqlServerName = '${prefix}-${suffix}'

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = { name: registryName }
resource environment 'Microsoft.App/managedEnvironments@2026-07-01' existing = { name: '${prefix}-env' }
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = { name: '${prefix}-api-identity' }
resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = { name: '${prefix}-worker-identity' }

resource api 'Microsoft.App/containerApps@2026-07-01' = {
  name: '${prefix}-api'
  location: location
  identity: {
	type: 'UserAssigned'
	userAssignedIdentities: { '${apiIdentity.id}': {} }
  }
  properties: {
	environmentId: environment.id
	configuration: {
	  activeRevisionsMode: 'Single'
	  ingress: {
		external: true
		allowInsecure: false
		targetPort: 5000
		transport: 'auto'
	  }
	  registries: [{ server: registry.properties.loginServer, identity: apiIdentity.id }]
	}
	template: {
	  containers: [
		{
		  name: 'api'
		  image: '${registry.properties.loginServer}/sieve-api:${imageTag}'
		  env: [
			{ name: 'Authentication__TenantId', value: tenantId }
			{ name: 'Authentication__Audience', value: apiClientId }
			{ name: 'ConnectionStrings__WorkDatabase', value: 'Server=tcp:${sqlServerName}${az.environment().suffixes.sqlServerHostname},1433;Initial Catalog=primes;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;User Id=${apiIdentity.properties.clientId}' }
		  ]
		  resources: { cpu: json('0.5'), memory: '1Gi' }
		  probes: [
			{
			  type: 'Liveness'
			  httpGet: { path: '/api/job/health', port: 5000, scheme: 'HTTP' }
			  initialDelaySeconds: 10
			  periodSeconds: 30
			}
		  ]
		}
	  ]
	  scale: { minReplicas: 1, maxReplicas: 1 }
	}
  }
}

resource worker 'Microsoft.App/jobs@2026-07-01' = {
  name: '${prefix}-worker'
  location: location
  identity: {
	type: 'UserAssigned'
	userAssignedIdentities: { '${workerIdentity.id}': {} }
  }
  properties: {
	environmentId: environment.id
	configuration: {
	  triggerType: 'Manual'
	  manualTriggerConfig: { parallelism: workerParallelism, replicaCompletionCount: workerParallelism }
	  replicaTimeout: 3600
	  replicaRetryLimit: 1
	  registries: [{ server: registry.properties.loginServer, identity: workerIdentity.id }]
	}
	template: {
	  containers: [
		{
		  name: 'worker'
		  image: '${registry.properties.loginServer}/sieve-worker:${imageTag}'
		  env: [
			{ name: 'SIEVE_SERVER_URL', value: 'https://${api.properties.configuration.ingress.fqdn}' }
			{ name: 'SIEVE_API_SCOPE', value: 'api://${apiClientId}/.default' }
			{ name: 'SIEVE_BATCH_SIZE', value: '1' }
			{ name: 'AZURE_CLIENT_ID', value: workerIdentity.properties.clientId }
		  ]
		  resources: { cpu: json('0.5'), memory: '1Gi' }
		}
	  ]
	}
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output workerJobName string = worker.name
