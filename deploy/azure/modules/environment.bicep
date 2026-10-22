// The Container Apps environment: the shared space the apps run in, placed
// in the apps subnet. Apps in one environment reach each other by name.
param location string
param tags object

@description('The subnet the environment\'s infrastructure and apps get their addresses from.')
param subnetId string

@description('The Log Analytics workspace the containers\' console output goes to.')
param workspaceId string

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: 'cae-finance-tracker'
  location: location
  tags: tags
  properties: {
    // A workload profiles environment: it can be placed in a /27 or larger
    // subnet, and its init containers can use a managed identity.
    // "Consumption" is the serverless profile, billed per second of use.
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    vnetConfiguration: {
      infrastructureSubnetId: subnetId
      // false: the environment gets a public entry point, so an app with
      // external ingress is reachable from the internet. Apps without it
      // stay private either way.
      internal: false
    }
    // Azure creates a second resource group for the environment's own
    // infrastructure (its load balancer and public address). Named here so
    // it is recognizable; it is deleted together with the environment.
    infrastructureResourceGroup: '${resourceGroup().name}-infrastructure'
    zoneRedundant: false
    // Console output goes through Azure Monitor's diagnostic settings below,
    // which need no workspace key, unlike the "log-analytics" destination.
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

// Diagnostic settings on a resource have no newer API version than this
// preview; the linter only finds the older 2016 one, of a different shape.
#disable-next-line use-recent-api-versions
resource consoleLogsToWorkspace 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  name: 'logs-to-workspace'
  properties: {
    workspaceId: workspaceId
    logs: [
      {
        // What the containers write to stdout and stderr.
        category: 'ContainerAppConsoleLogs'
        enabled: true
      }
      {
        // What the platform reports: pulls, starts, crashes, scaling.
        category: 'ContainerAppSystemLogs'
        enabled: true
      }
    ]
  }
}

output id string = environment.id
