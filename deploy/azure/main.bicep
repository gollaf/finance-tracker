// The finance tracker's Azure environment: the network, the database, the
// monitoring workspace and the identity the containers run as. Everything
// goes into one resource group, so deleting the group removes all of it.
targetScope = 'resourceGroup'

@description('Region for every resource. Defaults to the resource group\'s region.')
param location string = resourceGroup().location

@description('Tags put on every resource, to find them in the portal and on the bill.')
param tags object = {
  project: 'finance-tracker'
}

// The database server's name becomes a DNS name, so it must be unique
// across all of Azure. A hash of the resource group's ID is the same on
// every deploy into that group, and different for anyone else.
var suffix = take(uniqueString(resourceGroup().id), 6)

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    tags: tags
  }
}

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    location: location
    tags: tags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    tags: tags
    publisherPrincipalId: identity.outputs.principalId
  }
}

module database 'modules/database.bicep' = {
  name: 'database'
  params: {
    location: location
    tags: tags
    serverName: 'psql-finance-tracker-${suffix}'
    subnetId: network.outputs.databaseSubnetId
    privateDnsZoneId: network.outputs.databasePrivateDnsZoneId
    administratorPrincipalId: identity.outputs.principalId
    administratorName: identity.outputs.name
  }
}

@description('The client ID the containers pass to Azure.Identity to sign in as the apps identity.')
output appsIdentityClientId string = identity.outputs.clientId

@description('The database role the apps identity signs in to PostgreSQL as.')
output databaseUserName string = identity.outputs.name

@description('The database server\'s address, reachable only from inside the virtual network.')
output databaseHost string = database.outputs.host

// Not a secret: with key-based sign-in switched off, it only says where to
// send telemetry, and every request still needs an Entra token.
@description('Where the apps send telemetry.')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
