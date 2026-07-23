// Azure Container Registry — private Docker image storage
// SKU: Basic (dev), Standard (staging), Premium (prod — geo-replication + private endpoints).
// Features: admin user disabled, image scanning (Defender for Containers), retention policies.

@description('Environment name: dev, staging, or prod')
param environment string

@description('Location for the ACR')
param location string = resourceGroup().location

@description('Principal ID of the ACA Managed Identity for AcrPull role')
param acaManagedIdentityPrincipalId string

var skuMap = {
  dev: 'Basic'
  staging: 'Standard'
  prod: 'Premium'
}
var registryName = 'crinvoice${environment}'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: skuMap[environment]
  }
  properties: {
    adminUserEnabled: false                  // No admin user — Managed Identity only
    networkRuleSet: {
      defaultAction: 'Allow'
    }
    policies: {
      retentionPolicy: {
        days: 30
        status: 'enabled'
      }
      quarantinePolicy: {
        status: 'disabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
    }
  }
}

// Grant ACA Managed Identity AcrPull role
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, acaManagedIdentityPrincipalId, 'acrpull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')  // AcrPull
    principalId: acaManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('ACR login server URL')
output loginServer string = containerRegistry.properties.loginServer

@description('ACR resource ID')
output registryId string = containerRegistry.id
