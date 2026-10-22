// The finance tracker on Azure: the network, the database, monitoring, the
// identity the containers run as, and the containers themselves. Everything
// goes into one resource group, so deleting the group removes all of it.
targetScope = 'resourceGroup'

@description('Region for every resource. Defaults to the resource group\'s region.')
param location string = resourceGroup().location

@description('Tags put on every resource, to find them in the portal and on the bill.')
param tags object = {
  project: 'finance-tracker'
}

// Exactly the form CI tags images with, which also refuses "latest": an
// app pinned to a moving tag can't tell which build it runs.
@description('The build to run: "sha-" and the first 7 characters of a master commit, e.g. sha-1234567.')
@minLength(11)
@maxLength(11)
param imageTag string

// The database server's name becomes a DNS name, so it must be unique
// across all of Azure. A hash of the resource group's ID is the same on
// every deploy into that group, and different for anyone else.
var suffix = take(uniqueString(resourceGroup().id), 6)

// RabbitMQ has no Entra sign-in, so it keeps a password. Derived, not
// stored: the same on every deploy, so the broker and the Worker always
// agree, and unknown to anyone without access to this subscription. It
// exists only in the two apps' secrets, and the broker is reachable only
// from inside the Container Apps environment.
var rabbitMqPassword = guid(subscription().id, resourceGroup().id, 'rabbitmq-password')

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

module environment 'modules/environment.bicep' = {
  name: 'environment'
  params: {
    location: location
    tags: tags
    subnetId: network.outputs.appsSubnetId
    workspaceId: monitoring.outputs.workspaceId
  }
}

module apps 'modules/apps.bicep' = {
  name: 'apps'
  params: {
    location: location
    tags: tags
    environmentId: environment.outputs.id
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    imageTag: imageTag
    databaseHost: database.outputs.host
    databaseUserName: identity.outputs.name
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    appsSubnetPrefix: network.outputs.appsSubnetPrefix
    rabbitMqPassword: rabbitMqPassword
  }
}

@description('The Api\'s public address.')
output apiUrl string = apps.outputs.apiUrl

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
