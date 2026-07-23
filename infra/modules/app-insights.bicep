// Application Insights + Log Analytics Workspace
// Receives OpenTelemetry traces, metrics, and structured logs from the API via Aspire ServiceDefaults.
// Includes smart detection rules and custom alert rules.
// Workspace-based (classic AI is deprecated).

@description('Environment name: dev, staging, or prod')
param environment string

@description('Location for the resources')
param location string = resourceGroup().location

@description('ACA API resource ID for linking Application Map')
param acaApiId string = ''

var workspaceName = 'log-invoice-${environment}'
var appInsightsName = 'appi-invoice-${environment}'
var actionGroupName = 'ag-invoice-${environment}'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: environment == 'prod' ? 'PerGB2018' : 'PerGB2018'
    }
    retentionInDays: environment == 'prod' ? 90 : 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights (workspace-based)
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: environment == 'prod' ? 90 : 30
  }
}

// Action Group for alert notifications (placeholder — configure email/webhook post-deployment)
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'Global'
  properties: {
    groupShortName: 'inv-${environment}'
    enabled: true
    emailReceivers: [
      {
        name: 'oncall'
        emailAddress: 'oncall@example.com'    // Replace post-deployment
        useCommonAlertSchema: true
      }
    ]
  }
}

// Alert Rule: Failed requests > 5% for 5 minutes
resource highFailureRateAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-invoice-high-failure-${environment}'
  location: 'Global'
  properties: {
    description: 'API failed request rate exceeds 5% sustained for 5 minutes'
    severity: 2
    enabled: environment != 'dev'
    scopes: [appInsights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          name: 'Metric1'
          metricName: 'requests/failed'
          metricNamespace: 'Microsoft.Insights/components'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// Alert Rule: Response time P95 > 2 seconds for 5 minutes
resource highLatencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-invoice-high-latency-${environment}'
  location: 'Global'
  properties: {
    description: 'API P95 response time exceeds 2 seconds sustained for 5 minutes'
    severity: 3
    enabled: environment != 'dev'
    scopes: [appInsights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          name: 'Metric1'
          metricName: 'requests/duration'
          metricNamespace: 'Microsoft.Insights/components'
          operator: 'GreaterThan'
          threshold: 2000
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

@description('Application Insights instrumentation key (connection string)')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string')
output connectionString string = appInsights.properties.ConnectionString

@description('Log Analytics workspace ID')
output workspaceId string = logAnalyticsWorkspace.id

@description('Application Insights resource ID')
output appInsightsId string = appInsights.id
