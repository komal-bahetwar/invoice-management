// Azure Cache for Redis — caching layer (future)
// SKU: Basic C0 250MB (staging), Standard C1 1GB (prod).
// Pattern: cache-aside for dashboard queries (TTL-based invalidation on invoice mutations).
// Not provisioned in dev to save cost.

@description('Environment name: dev, staging, or prod')
param environment string

@description('Location for Redis')
param location string = resourceGroup().location

@description('Provision Redis? Skipped for dev to save cost.')
param provisionRedis bool = environment != 'dev'

var skuConfig = {
  dev: { name: 'Basic', family: 'C', capacity: 0 }
  staging: { name: 'Basic', family: 'C', capacity: 0 }    // 250 MB
  prod: { name: 'Standard', family: 'C', capacity: 1 }     // 1 GB
}

resource redis 'Microsoft.Cache/redis@2024-04-01-preview' = if (provisionRedis) {
  name: 'redis-invoice-${environment}'
  location: location
  properties: {
    sku: {
      name: skuConfig[environment].name
      family: skuConfig[environment].family
      capacity: skuConfig[environment].capacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisConfiguration: {
      'maxmemory-policy': 'volatile-lru'
      'maxfragmentationmemory-reserved': environment == 'prod' ? '100' : '50'
      'maxmemory-reserved': environment == 'prod' ? '100' : '50'
    }
    publicNetworkAccess: 'Enabled'
  }
}

@description('Redis host name')
output hostName string = provisionRedis ? redis.properties.hostName : ''

@description('Redis primary key (SSL)')
output primaryKey string = provisionRedis ? redis.listKeys().primaryKey : ''

@description('Redis SSL port')
output sslPort int = 6380
