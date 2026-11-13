targetScope = 'resourceGroup'

@description('Azure region for the one-time execution.')
param location string = 'westus3'

@description('Registry login server from the registry deployment.')
param registryServer string

@description('SHA-256 digest of the tested one-shot image, including the sha256: prefix.')
param imageDigest string

@description('Resource ID of the user-assigned identity with registry pull access.')
param pullIdentityId string

resource calculation 'Microsoft.ContainerInstance/containerGroups@2026-07-01' = {
  name: 'largest-56-bit-prime'
  location: location
  identity: {
	type: 'UserAssigned'
	userAssignedIdentities: { '${pullIdentityId}': {} }
  }
  properties: {
	osType: 'Linux'
	restartPolicy: 'Never'
	imageRegistryCredentials: [{ server: registryServer, identity: pullIdentityId }]
	containers: [
	  {
		name: 'calculation'
		properties: {
		  image: '${registryServer}/sieve-oneshot@${imageDigest}'
		  resources: { requests: { cpu: 1, memoryInGB: 1 } }
		}
	  }
	]
  }
}

output containerGroupName string = calculation.name
