// Azure Service Bus Namespace + Topics
// Used by MassTransit with Outbox pattern for domain event publishing.
// Topics: invoice-events (InvoiceCreated, InvoiceStatusChanged), tenant-events (TenantProvisioned)
// SKU: Standard (dev/staging), Premium (prod — VNet integration + availability zones)

@description('Environment name: dev, staging, or prod')
param environment string

@description('Location for the Service Bus namespace')
param location string = resourceGroup().location

@description('Name of the Log Analytics workspace for diagnostics')
param logAnalyticsWorkspaceId string

var sku = environment == 'prod' ? 'Premium' : 'Standard'
var namespaceName = 'sb-invoice-${environment}'
var topicNames = [
  'invoice-events'
  'tenant-events'
]

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  sku: {
    name: sku
    tier: sku
    capacity: sku == 'Premium' ? 1 : null
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// Topics for domain events
resource topics 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = [for topicName in topicNames: {
  name: topicName
  parent: serviceBusNamespace
  properties: {
    defaultMessageTimeToLive: 'P14D'          // 14-day TTL
    maxSizeInMegabytes: 1024                  // 1 GB
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    supportOrdering: false
  }
}]

// Authorization Rule — send-only for API, listen for future consumers
resource sendRule 'Microsoft.ServiceBus/namespaces/topics/authorizationRules@2024-01-01' = [for topicName in topicNames: {
  name: 'send-rule'
  parent: topics[indexOf(topicNames, topicName)]
  properties: {
    rights: ['Send']
  }
}]

resource listenRule 'Microsoft.ServiceBus/namespaces/topics/authorizationRules@2024-01-01' = [for topicName in topicNames: {
  name: 'listen-rule'
  parent: topics[indexOf(topicNames, topicName)]
  properties: {
    rights: ['Listen']
  }
}]

// Diagnostic settings — send metrics/logs to Log Analytics
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'servicebus-diagnostics'
  scope: serviceBusNamespace
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'OperationalLogs', enabled: true }
      { category: 'VNetAndIPFilteringLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

@description('Service Bus namespace primary connection string (send-only)')
output primaryConnectionString string = serviceBusNamespace.listKeys().primaryConnectionString

@description('Service Bus namespace name')
output namespaceName string = serviceBusNamespace.name

@description('Service Bus namespace resource ID')
output namespaceId string = serviceBusNamespace.id
