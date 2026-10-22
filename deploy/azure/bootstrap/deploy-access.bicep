// What the deploy identity may do, limited to rg-finance-tracker.
targetScope = 'resourceGroup'

@description('Object ID of the deploy identity.')
param principalId string

var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var roleAssignmentAdministratorRoleId = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'

// The one role the template assigns: Monitoring Metrics Publisher, for the
// apps identity on Application Insights.
var assignableRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

// Create, change and delete resources in the group. Contributor can't
// assign roles.
resource contributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, contributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Assign and remove roles in the group, but, by the condition, only the one
// role the template needs. Without it, the deploy identity could make
// itself or anyone else Owner of the group.
resource roleAssignmentAdministrator 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, roleAssignmentAdministratorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleAssignmentAdministratorRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
    conditionVersion: '2.0'
    condition: '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignableRoleId}})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignableRoleId}}))'
  }
}
