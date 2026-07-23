# Azure Infrastructure as Code

> Bicep templates for provisioning Azure production resources for the Invoice Management API.

## Structure

```
docs/infra/
├── README.md
├── modules/           # Bicep modules (one per Azure resource)
│   ├── service-bus.bicep
│   ├── key-vault.bicep
│   ├── app-insights.bicep
│   ├── container-registry.bicep
│   └── redis.bicep
└── parameters/        # Environment-specific parameter files
    ├── dev.parameters.json
    ├── staging.parameters.json
    └── prod.parameters.json
```

## Resources

| Module | Azure Resource | Purpose |
|---|---|---|
| `service-bus.bicep` | Azure Service Bus | MassTransit messaging (invoice-events, tenant-events topics) |
| `key-vault.bicep` | Azure Key Vault | Secrets: connection strings, signing keys, API keys |
| `app-insights.bicep` | App Insights + Log Analytics | OpenTelemetry traces, metrics, structured logs |
| `container-registry.bicep` | Azure Container Registry | Private Docker image storage, vulnerability scanning |
| `redis.bicep` | Azure Cache for Redis | Dashboard caching, tenant config caching |
