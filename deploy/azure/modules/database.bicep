// PostgreSQL in the database subnet, with no public address and no
// passwords: the only way in is a Microsoft Entra token for the apps
// identity, which is the server's administrator.
param location string
param tags object
param serverName string
param subnetId string
param privateDnsZoneId string

@description('Object ID of the identity that administers the server and that the apps sign in as.')
param administratorPrincipalId string

@description('That identity\'s name, which becomes its role name in PostgreSQL.')
param administratorName string

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: serverName
  location: location
  tags: tags
  // The smallest size, and the one the free account's 12-month offer covers.
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    // The version every other environment runs.
    version: '16'
    storage: {
      // The free offer's storage. Without auto-grow, a full disk stops writes
      // instead of growing the bill.
      storageSizeGB: 32
      autoGrow: 'Disabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    // Private access: an address in the delegated subnet, published in the
    // private DNS zone. Can't be changed to public access later.
    network: {
      delegatedSubnetResourceId: subnetId
      privateDnsZoneArmResourceId: privateDnsZoneId
    }
    // Microsoft Entra ID only: no PostgreSQL passwords exist, so no
    // administrator login is given.
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
  }
}

// The Entra administrator: the apps identity. Its resource name is the
// identity's object ID; principalName becomes the role name.
resource administrator 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = {
  parent: server
  name: administratorPrincipalId
  properties: {
    principalType: 'ServicePrincipal'
    principalName: administratorName
    tenantId: tenant().tenantId
  }
}

output host string = server.properties.fullyQualifiedDomainName
