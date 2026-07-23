// Azure Key Vault — secrets management
// Uses Managed Identity RBAC (not access policies) — ACA gets 'Key Vault Secrets User' role.
// Stores: SQL connection strings, Service Bus connection strings, IdentityServer signing keys, API keys.
// SKU: Standard for all environments.

@description('Environment name: dev, staging, or prod')
param environment string

@description('Location for the Key Vault')
param location string = resourceGroup().location

@description('Tenant ID for RBAC')
param tenantId string = subscription().tenantId

@description('Principal ID of the ACA Managed Identity that needs secret read access')
param acaManagedIdentityPrincipalId string

@description('Log Analytics workspace ID for audit diagnostics')
param logAnalyticsWorkspaceId string

var vaultName = 'kv-invoice-${environment}'
var secretNames = [
  'sql-connection-string'
  'service-bus-connection-string'
  'identityserver-signing-key'
]

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true           // RBAC, not access policies
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Grant ACA Managed Identity the Key Vault Secrets User role (RBAC)
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vaultName, acaManagedIdentityPrincipalId, 'secrets-user')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')  // Key Vault Secrets User
    principalId: acaManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Placeholder secrets — actual values set post-deployment via CI or manual
resource secrets 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = [for secretName in secretNames: {
  name: secretName
  parent: keyVault
  properties: {
    value: 'placeholder-${secretName}'      // Replace post-deployment
    attributes: {
      enabled: true
    }
  }
}]

// Audit logging — all secret access events
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'keyvault-diagnostics'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'AuditEvent', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

@description('Key Vault URI')
output vaultUri string = keyVault.properties.vaultUri

@description('Key Vault resource ID')
output vaultId string = keyVault.id
