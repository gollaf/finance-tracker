// The identity GitHub Actions deploys as, and the federated credential that
// lets a GitHub job become it: Entra ID accepts a token GitHub signed, if
// its issuer, audience and subject match exactly.
param location string
param tags object
param githubSubject string

resource deployer 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-finance-tracker-deploy'
  location: location
  tags: tags
}

resource githubAzureEnvironment 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: deployer
  name: 'github-azure-environment'
  properties: {
    // GitHub's OpenID Connect issuer: who signs the token.
    issuer: 'https://token.actions.githubusercontent.com'
    // Which repository, and which environment in it, the job ran in. Only
    // jobs that waited for the environment's approval get this subject.
    subject: githubSubject
    // Who the token is meant for: Entra ID's token exchange.
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output principalId string = deployer.properties.principalId
output clientId string = deployer.properties.clientId
