// The identity the Api and the Worker run as. A user-assigned identity is a
// resource of its own, so it exists before the containers do: the database
// and Application Insights can trust it before anything runs.
param location string
param tags object

resource apps 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-finance-tracker-apps'
  location: location
  tags: tags
}

output name string = apps.name

@description('Its object ID in Microsoft Entra ID: what role assignments and the database refer to.')
output principalId string = apps.properties.principalId

@description('Its client ID: what code passes when asking for a token as this identity.')
output clientId string = apps.properties.clientId
