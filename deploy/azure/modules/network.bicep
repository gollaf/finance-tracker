// A private network with one subnet for the containers and one for the
// database, and the private DNS zone that resolves the database's name
// inside it.
param location string
param tags object

// 10.40.0.0/16 overlaps none of the ranges Container Apps reserves for
// itself. Subnets can't be resized once something runs in them.
var addressSpace = '10.40.0.0/16'
var appsSubnetPrefix = '10.40.0.0/24'
var databaseSubnetPrefix = '10.40.1.0/28'

// The subnets are declared inside the network, not as separate resources:
// a redeploy of a network whose subnets live elsewhere tries to remove them.
resource vnet 'Microsoft.Network/virtualNetworks@2025-07-01' = {
  name: 'vnet-finance-tracker'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressSpace
      ]
    }
    subnets: [
      {
        // For the Container Apps environment. Delegated: only it may use
        // the subnet. /27 is the minimum; /24 leaves room to scale.
        name: 'snet-apps'
        properties: {
          addressPrefix: appsSubnetPrefix
          delegations: [
            {
              name: 'container-apps'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        // For the PostgreSQL server only. /28 is the smallest allowed.
        name: 'snet-database'
        properties: {
          addressPrefix: databaseSubnetPrefix
          delegations: [
            {
              name: 'postgresql'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
    ]
  }
}

// The server's address inside the network is published here. The name must
// end in .postgres.database.azure.com and must not be the server's own name.
resource databaseDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'finance-tracker.private.postgres.database.azure.com'
  location: 'global'
  tags: tags
}

// Without the link, nothing in the network can resolve the zone's names.
resource databaseDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: databaseDnsZone
  name: vnet.name
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

output appsSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-apps')
output databaseSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-database')

// A module's outputs exist only once all of it is deployed, so whoever
// uses the zone through this output also waits for the link above.
output databasePrivateDnsZoneId string = databaseDnsZone.id
