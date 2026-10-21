// Where telemetry is kept: a Log Analytics workspace (the storage and query
// engine) and an Application Insights resource on top of it (the views for
// requests, traces and failures).
param location string
param tags object

@description('Object ID of the identity allowed to send telemetry.')
param publisherPrincipalId string

// Azure's built-in "Monitoring Metrics Publisher" role. Despite its name it
// allows sending all telemetry, not only metrics.
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'log-finance-tracker'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // Included in the price; longer retention is billed.
    retentionInDays: 30
    // A runaway log loop stops being stored after 1 GB in a day, instead
    // of using up the credit.
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-finance-tracker'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    // Telemetry is accepted only with a Microsoft Entra token, not with the
    // key inside the connection string alone.
    DisableLocalAuth: true
  }
}

resource publisherCanSendTelemetry 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: appInsights
  // Must be a GUID, and the same on every deploy, or a second assignment
  // would be attempted and refused.
  name: guid(appInsights.id, publisherPrincipalId, monitoringMetricsPublisherRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: publisherPrincipalId
    // Says up front what kind of principal this is. Without it, Azure looks
    // the identity up first, and a just-created one may not be found yet.
    principalType: 'ServicePrincipal'
  }
}

output appInsightsConnectionString string = appInsights.properties.ConnectionString
output workspaceId string = workspace.id
