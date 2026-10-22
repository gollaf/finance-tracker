// One-time setup, deployed from the laptop by the subscription's owner:
// the two resource groups, and the identity GitHub Actions deploys as,
// trusted to sign in from this repository's "azure" environment without
// any stored key. main.bicep in the folder above is then deployed into
// rg-finance-tracker.
targetScope = 'subscription'

@description('Region for the resource groups and the identity: the one given to `az deployment sub create --location`.')
param location string = deployment().location

@description('The exact subject GitHub puts in the sign-in token of a job in the "azure" environment, as the Azure sign-in check workflow prints it.')
param githubSubject string

param tags object = {
  project: 'finance-tracker'
}

// Everything the system needs. Deleting it is the teardown.
resource appGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-finance-tracker'
  location: location
  tags: tags
}

// Only the deploy identity, so a teardown of the system leaves it, and its
// trust with GitHub, in place.
resource ciGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: 'rg-finance-tracker-ci'
  location: location
  tags: tags
}

module deployIdentity 'deploy-identity.bicep' = {
  scope: ciGroup
  name: 'deploy-identity'
  params: {
    location: location
    tags: tags
    githubSubject: githubSubject
  }
}

module deployAccess 'deploy-access.bicep' = {
  scope: appGroup
  name: 'deploy-access'
  params: {
    principalId: deployIdentity.outputs.principalId
  }
}

// The three values the GitHub environment needs, as secrets of the same
// names. None of them is a credential: signing in also needs a token that
// only GitHub can issue.
output AZURE_CLIENT_ID string = deployIdentity.outputs.clientId
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
