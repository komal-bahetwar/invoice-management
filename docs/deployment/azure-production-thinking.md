# Azure Production Thinking — Invoice Management API

## 1. Overview & Architecture

This document details the production Azure deployment strategy for the **Multi-tenant Invoice Management API**. It goes beyond the summary in `SOLUTION_NOTES.md` to cover best practices, scaling, rollbacks, disaster recovery, and resource utilisation with production-grade depth.

### Why .NET Aspire + Azure Container Apps?

The project uses **.NET Aspire** (`AppHost.cs`) for local development orchestration — provisioning SQL Server and Seq containers, injecting connection strings, and wiring OpenTelemetry. **Azure Container Apps (ACA)** is the native, first-class production target for .NET Aspire. Running `azd up` reads the same `AppHost.cs` and provisions the equivalent Azure services, eliminating the dev/prod gap entirely.

ACA is **serverless Kubernetes** — built on AKS, managed by Microsoft. You get containerised workloads, horizontal autoscaling, revision-based deployments, and managed ingress, without managing node pools, RBAC, or YAML manifests.

### Azure Resource Hierarchy

```
Subscription
└── Resource Group: rg-invoice-{env}    (env = dev | staging | prod)
    ├── Azure Container Apps Environment
    │   ├── Container App: invoice-api        (the API itself)
    │   └── Container App: invoice-migrator   (one-shot DB migration job)
    ├── Azure SQL Database (Serverless or Provisioned)
    │   └── Elastic Pool (for multi-tenant cost efficiency)
    ├── Azure Service Bus Namespace
    │   └── Topics: invoice-events, tenant-events
    ├── Azure Container Registry
    ├── Azure Key Vault
    ├── Azure Cache for Redis (future: dashboard caching)
    ├── Application Insights
    └── Log Analytics Workspace
```

---

## 2. Azure Services Deep-Dive

### Azure Container Apps (ACA) — Compute

**Why ACA over App Service or AKS:**

| Option | ACA Advantage |
|---|---|
| App Service | App Service is a single-app PaaS. It doesn't understand .NET Aspire's multi-container topology — you'd bypass the `AppHost` investment entirely and manage all infra manually. |
| AKS | AKS requires a platform engineering team to manage clusters, node pools, networking, and RBAC. For this project's scope, it's overkill with a ~$70+/mo minimum cost even when idle. ACA provides the same containerised experience without the operational tax. |

ACA is the pragmatic default. AKS is the natural evolution path (see Section 8) — when the time comes, migrating ACA → AKS is straightforward since both run the same containers.

**ACA features in use:**
- **Revision management**: Each deployment creates an immutable revision. Rollback = reactivate an older revision (instant, no rebuild).
- **Traffic splitting**: Canary deployments by shifting X% of traffic to a new revision, then ramping to 100%.
- **Scale-to-zero**: Dev/staging environments scale to 0 replicas when idle, reducing cost to near-zero.
- **Managed Ingress**: TLS termination, custom domain support — no need to manage an ingress controller manually.
- **Secrets via Dapr**: ACA integrates with Dapr for secret management, though Key Vault + Managed Identity is preferred for this project.

### Azure SQL Database — Persistence

**Why managed SQL over SQL Server in a Container:**

Running SQL Server in a container is acceptable for local dev (Aspire handles this). In production, Azure SQL Database provides:
- **Automated backups**: Full weekly, differential daily, log backups every 5–10 minutes. Up to 35 days of point-in-time restore.
- **Geo-replication**: Active geo-replication to a paired region for disaster recovery.
- **Elastic pools**: Share DTU/vCore resources across multiple tenant databases — far more cost-efficient than provisioning per-tenant instances.
- **VNet integration**: Private endpoints ensure SQL traffic never leaves the Azure backbone.
- **Built-in threat detection**: SQL injection alerts, vulnerability assessments, data classification.

**Tenant isolation** is achieved via schema-per-tenant (`tenant_{sanitized-id}`) using Finbuckle.MultiTenant. Each tenant's data lives in its own schema within the same database. This provides database-level isolation without the overhead of per-tenant databases. For high-tier tenants requiring dedicated resources, they can be moved to their own elastic pool or database.

### Azure Service Bus — Messaging

**Why Service Bus over RabbitMQ in a container:**

The project uses **MassTransit with Outbox pattern** for domain event publishing. Locally, MassTransit uses an in-memory transport. In production:
- **Service Bus** provides FIFO ordering, duplicate detection, dead-letter queues, and scheduled delivery — critical for reliable event-driven flows (invoice created → notify → audit).
- **Transactional Outbox**: Events are persisted in the same database transaction as the domain change. A background processor publishes them to Service Bus. This guarantees at-least-once delivery even if Service Bus is temporarily unavailable.
- **Topics**: `invoice-events` (InvoiceCreated, InvoiceStatusChanged) and `tenant-events` (TenantProvisioned) for future consumers.

### Azure Container Registry (ACR) — Image Storage

- Private Docker registry for CI/CD-built container images.
- **Image scanning**: Microsoft Defender for Containers scans images for vulnerabilities before deployment.
- **Retention policies**: Untag and delete old images to control storage costs.
- **Geo-replication**: Replicate images to regions close to deployment targets for faster pulls.
- **ACR Tasks**: Optionally build images in the cloud (no local Docker daemon needed in CI).

### Azure Key Vault — Secrets

- **All sensitive values** go to Key Vault: SQL connection strings, Service Bus connection strings, IdentityServer signing keys, API keys.
- **Managed Identity RBAC**: The Container App gets a User-Assigned Managed Identity with `Key Vault Secrets User` role — it reads secrets at startup without any connection string to Key Vault itself.
- **Key rotation**: IdentityServer token signing keys can be rotated by updating the Key Vault secret and restarting the revision (ACA does this automatically on deployment).
- **Audit logging**: Every secret access is logged — who accessed what, when, and from which IP.

### Application Insights + Azure Monitor — Observability

- **OpenTelemetry (OTel)**: Aspire's `ServiceDefaults` project configures OTel tracing, metrics, and logging. These flow natively to Application Insights with **zero production code changes**.
- **Application Map**: Automatically builds a topology of API → SQL → Service Bus with latency and error rates.
- **Custom metrics**: Business KPIs (invoices created/minute, status transition latency) can be emitted as custom metrics.
- **Alert rules**: Failed requests > 5% in 5 minutes → on-call notification. DB DTU > 80% for 10 minutes → scale-up warning.
- **Log Analytics**: All Serilog structured logs are queryable via KQL for debugging.

### Azure Cache for Redis (Future)

- Cache-aside for dashboard responses (TTL-based invalidation on invoice mutations).
- Tenant configuration cache to reduce DB load on every request.
- Session store if sticky sessions are needed.

---

## 3. Deployment Workflow & CI/CD

### Local Development (Today)

```bash
dotnet run --project backend/src/InvoiceManagement.AppHost
```

Aspire provisions:
- SQL Server container (with `WithDataVolume()` for persistence)
- Seq container (structured log viewer)
- API project with auto-injected connection strings

The [Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard) shows structured logs, distributed traces, and environment variables for all services.

### One-Time Azure Setup

```bash
# In the solution root
azd init
```

This inspects `AppHost.cs`, detects SQL Server + API, and generates:
- `azure.yaml` — maps Aspire resources to Azure services
- `infra/` — Bicep templates for all Azure resources (reviewable and version-controlled)

Run `azd infra synth` to inspect the generated Bicep before provisioning.

### Dev/Test Deployment

```bash
azd up
```

This single command:
1. Provisions the Resource Group, ACA Environment, SQL Database, Service Bus, ACR, Key Vault, App Insights
2. Builds the API and Migrator as Docker containers
3. Pushes images to ACR
4. Deploys to ACA with Managed Identity (passwordless)
5. Outputs the public URL

### Production CI/CD Pipeline

```bash
azd pipeline config
```

This bootstraps a **GitHub Actions** workflow with OIDC authentication (no stored Azure credentials — GitHub's OIDC provider trusts Azure AD).

**Pipeline stages on merge to `main`:**

```
┌──────────┐    ┌──────────┐    ┌─────────────┐    ┌──────────────┐    ┌─────────────┐
│ Build    │ →  │ Test     │ →  │ Publish      │ →  │ Run Migrator │ →  │ Deploy ACA  │
│ dotnet   │    │ dotnet   │    │ Docker image │    │ (one-shot    │    │ (new         │
│ build    │    │ test     │    │ push to ACR  │    │  container)  │    │  revision)   │
└──────────┘    └──────────┘    └─────────────┘    └──────────────┘    └─────────────┘
                                                                              │
                                                                              ▼
                                                                       ┌─────────────┐
                                                                       │ Smoke Tests │
                                                                       │ (health +   │
                                                                       │  key APIs)  │
                                                                       └─────────────┘
                                                                              │
                                                                              ▼
                                                                       ┌─────────────┐
                                                                       │ Traffic     │
                                                                       │ Shift 100%  │
                                                                       │ + Deactivate│
                                                                       │ old revision│
                                                                       └─────────────┘
```

**Key details:**
- **Migrator runs first**: A one-shot Container App job runs EF Core migrations. If it fails, the deployment stops — no API revision is created against a stale schema.
- **Revision-based deployment**: ACA creates a new inactive revision, runs smoke tests against it, then shifts traffic. Zero downtime.
- **OIDC authentication**: GitHub Actions authenticates to Azure without stored secrets — the trust is established at the Azure AD application level.

### Multi-Environment Strategy

| Environment | Resource Group | Purpose | Scale |
|---|---|---|---|
| `dev` | `rg-invoice-dev` | Developer sandbox, feature branches | Scale-to-zero, minimal SQL |
| `staging` | `rg-invoice-staging` | Pre-production validation, load testing | 1 replica always-on, elastic pool |
| `prod` | `rg-invoice-prod` | Live SaaS tenants | 2+ replicas, provisioned SQL, geo-replication |

`azd` manages environments natively:
```bash
azd env new staging
azd env select staging
azd up  # deploys to staging resource group
```

---

## 4. Best Practices

### Security

| Practice | Implementation |
|---|---|
| **Passwordless connections** | Managed Identity for ACA → SQL, ACA → Service Bus, ACA → Key Vault. No connection strings in code, config, or environment variables. |
| **Key Vault RBAC** | Access policies are role-based, not access-policy-based. Only the Managed Identity gets `Secrets User`. |
| **HTTPS-only** | ACA Managed Ingress enforces TLS 1.2+. HTTP → HTTPS redirect is automatic. |
| **SQL firewall + VNet** | Azure SQL uses Private Endpoint — traffic stays on Azure backbone, never exposed to the internet. |
| **Container image signing** | Sign images in ACR with Notary or Sigstore for supply chain integrity. |
| **Least privilege SQL** | The API's identity has `db_datareader` + `db_datawriter` only — no DDL permissions. Only the Migrator identity has DDL. |
| **Dapr secret store** | ACA's Dapr integration can pull secrets from Key Vault at container startup, avoiding secret files in the container. |

### Resilience

- **Polly via Aspire ServiceDefaults**: The `ServiceDefaults` project configures:
  - **Retry**: Transient HTTP and SQL failures → exponential backoff (3 retries).
  - **Circuit breaker**: After 5 consecutive failures, trip the circuit for 30 seconds → fail fast instead of cascading.
  - **Timeout**: All external calls have a 30-second timeout.
- **Service Bus retry**: MassTransit retries transient failures with exponential backoff. Poison messages go to dead-letter queue after 5 attempts.
- **SQL connection resiliency**: EF Core configured with `EnableRetryOnFailure()` — handles Azure SQL transient faults transparently.

### Tenant Isolation at Infrastructure Level

- **Schema-per-tenant**: Each tenant's data lives in `tenant_{sanitized-id}` schema. Finbuckle.MultiTenant routes all EF Core queries to the correct schema per request.
- **No cross-tenant queries**: The `X-Tenant-Id` header is validated before any data access. A request without a valid tenant ID is rejected with 401/403.
- **Connection pooling per tenant**: EF Core pools connections, but each query is scoped to the correct schema. No tenant sees another's data.

### Cost Optimization

- **Scale-to-zero**: Dev/staging ACA environments scale to 0 when idle (nights/weekends). Production minimum is 1 replica.
- **Azure SQL elastic pools**: Share resources across tenants instead of provisioning per-tenant instances.
- **Reserved capacity**: Commit to 1-3 year reservations for production SQL and ACA for 40-60% savings.
- **Dev/Test pricing**: Use Azure Dev/Test subscription for non-production environments (significant discounts on SQL, ACA).
- **Auto-shutdown**: Runbook to stop non-production resources during off-hours (Azure Automation + tags).

---

## 5. Scaling Strategy

### ACA Autoscaling

| Rule | Threshold | Action |
|---|---|---|
| **HTTP concurrent requests** | > 100 req/instance for 1 minute | Scale out by 1 replica |
| **CPU utilisation** | > 70% for 5 minutes | Scale out by 1 replica |
| **Memory utilisation** | > 80% for 5 minutes | Scale out by 1 replica |
| **Low traffic** | < 10 req/instance for 10 minutes | Scale in by 1 replica |

**Scale limits:**
- Production: Min 2 replicas (HA), max 10 replicas
- Staging: Min 0 replicas (scale-to-zero), max 3 replicas
- Dev: Min 0 replicas, max 2 replicas

**Future: KEDA scalers** — ACA supports KEDA for custom scaling triggers. When Service Bus consumers are implemented, scale based on queue depth (e.g., +1 replica per 100 messages in the invoice-events queue).

### Database Scaling

**Phase 1 (Current):** Single elastic pool with all tenant schemas. Covers up to ~50 tenants with standard workload.

**Phase 2 (Growth):** Premium elastic pool with isolated resource governance per tenant database. Move high-volume tenants to dedicated resources.

**Phase 3 (Scale):** Read replicas for dashboard queries. Dashboard reads hit read replica; writes + transactional reads hit primary. Replication lag is typically <1 second.

### Caching Strategy (Future)

| Cache Key | TTL | Invalidation |
|---|---|---|
| `dashboard:{tenantId}` | 60 seconds | Cleared on InvoiceCreated, InvoiceStatusChanged events |
| `invoice:{tenantId}:{invoiceId}` | 5 minutes | Cleared on InvoiceStatusChanged |
| `tenant-config:{tenantId}` | 30 minutes | Cleared on tenant configuration update |

Redis cache-aside pattern: Check cache → miss → query DB → populate cache → return. Dashboard queries (aggregations) benefit most — they're the most expensive and most frequently accessed.

### Tenant Onboarding Automation

When a new tenant is provisioned:
1. **Schema creation**: A background job (triggered by `TenantProvisioned` event) runs `CREATE SCHEMA tenant_{id}` and applies migrations.
2. **Connection management**: Tenant metadata stored in a shared `__EFMigrationsHistory` table. Finbuckle resolves the schema at request time.
3. **Cache priming**: Pre-populate empty dashboard cache entry.
4. **Monitoring**: Tenant-specific metrics (request count, error rate) tracked via `tenantId` dimension in App Insights.

---

## 6. Rollback & Disaster Recovery

### Application Rollback via ACA Revisions

ACA maintains a configurable revision history (default: 100 inactive revisions). Rollback is **instant and zero-downtime**:

1. Identify the healthy revision: `az containerapp revision list -n invoice-api -g rg-invoice-prod`
2. Activate it: `az containerapp revision activate --revision <revision-name>`
3. Deactivate the bad revision: `az containerapp revision deactivate --revision <bad-revision>`

No rebuild, no redeploy — the old container image is already registered. Traffic shifts immediately.

### Canary Deployments

For high-risk changes, use **traffic splitting** instead of direct 100% shift:

```
Revision v5 (stable):  90% traffic
Revision v6 (canary):  10% traffic
```

Monitor v6 for 15 minutes (error rate, latency, CPU). If healthy, ramp to 50% → 100%. If unhealthy, shift 100% back to v5. All controlled via `az containerapp ingress traffic set`.

### Database Rollback

**Point-in-time restore**: Azure SQL keeps log backups every 5–10 minutes for up to 35 days. Restore to a point before the bad deployment:

```bash
az sql db restore --dest-name invoice-db-restored --name invoice-db \
  --resource-group rg-invoice-prod --server invoice-sql \
  --time "2026-07-23T14:00:00Z"
```

**Important**: Schema-per-tenant means a single database restore covers all tenants. If only one tenant's data is corrupted, restore to a separate database and copy the specific schema.

### Backup Strategy

| Backup Type | Frequency | Retention |
|---|---|---|
| Full backup | Weekly | 4 weeks (included with Azure SQL) |
| Differential backup | Every 12 hours | 4 weeks |
| Transaction log backup | Every 5–10 minutes | 35 days (point-in-time restore window) |
| Long-term retention (LTR) | Monthly full | 12 months (separate LTR policy, stored in geo-redundant storage) |

All backups are encrypted at rest and stored in geo-redundant storage (RA-GRS) by default.

### Disaster Recovery Plan

**Target:** RTO (Recovery Time Objective) < 1 hour, RPO (Recovery Point Objective) < 5 minutes.

**Active-passive across Azure regions:**

```
Primary: East US                   Secondary: West Europe
┌──────────────────────┐            ┌──────────────────────┐
│ ACA (active)         │            │ ACA (stopped/minimal) │
│ SQL (active)         │─── async ─→│ SQL (geo-replica)     │
│ Service Bus (active) │   geo-rep  │ Service Bus (geo-pair)│
│ ACR (active)         │            │ ACR (geo-replica)     │
└──────────────────────┘            └──────────────────────┘
```

**Failover procedure:**
1. Detect: Application Insights alerts on sustained API unavailability (>5 minutes) in primary region.
2. Decide: On-call engineer confirms region outage, triggers failover.
3. Failover SQL: Force geo-replica in West Europe to become primary.
4. Activate ACA: Scale secondary ACA environment to production replica count.
5. Update DNS: Azure Traffic Manager (or Front Door) routes traffic to West Europe endpoint.
6. Verify: Smoke tests pass, monitoring shows healthy traffic in secondary region.
7. Communicate: Status page updated, incident opened.

**Post-failover:** Once primary region recovers, reverse-replicate data back, then failback during a low-traffic window.

---

## 7. Resource Utilisation

### Right-Sizing by Environment

| Resource | Dev SKU | Staging SKU | Prod SKU |
|---|---|---|---|
| **ACA** | Consumption-only (0.5 vCPU, 1GB mem) | Consumption-only (1 vCPU, 2GB mem) | Dedicated D4 (4 vCPU, 8GB mem) per replica |
| **Azure SQL** | Serverless Gen5 (2 vCore) auto-pause | Serverless Gen5 (4 vCore) | Provisioned Gen5 (8 vCore) elastic pool |
| **Service Bus** | Standard | Standard | Premium (for VNet integration + availability zones) |
| **ACR** | Basic | Standard | Premium (geo-replication + private endpoints) |
| **Key Vault** | Standard | Standard | Standard |
| **Redis** | N/A | Basic C0 (250MB) | Standard C1 (1GB) with data persistence |

### Approximate Monthly Cost (Prod)

| Resource | SKU | Est. Monthly Cost |
|---|---|---|
| ACA (2 replicas, D4) | Dedicated | ~$170 |
| Azure SQL (8 vCore elastic pool) | Provisioned | ~$450 |
| Service Bus (Premium) | 1 MU | ~$70 |
| ACR (Premium) | Per GB storage | ~$20 |
| Key Vault | Standard | <$5 |
| App Insights | Pay-per-GB ingested | ~$30 |
| **Total (est.)** | | **~$745/month** |

**Cost-saving notes:**
- 3-year reserved capacity: ~40% off SQL and ACA → ~$450/month.
- Dev/Test subscription: ~50% off SQL and ACA for non-prod → dev/staging ~$50/month combined.
- Auto-shutdown non-prod during off-hours: Additional ~60% savings.

### Monitoring-Driven Optimization

App Insights data drives continuous right-sizing:

1. **CPU/Memory trending**: If average CPU is <20% over 30 days, consider reducing ACA vCPU allocation.
2. **DB DTU trending**: If peak DTU never exceeds 40%, downsize elastic pool or switch to serverless.
3. **Request latency**: If P95 latency is increasing, check for N+1 queries, missing indexes, or cache misses.
4. **Error rates**: Alert on error rate spikes; investigate correlation with deployments or load.
5. **Cost anomaly detection**: Azure Cost Management alerts if daily spend deviates >20% from baseline.

---

## 8. Future Evolution to AKS

ACA is the right choice for the current project scope. **Azure Kubernetes Service (AKS)** becomes the target when the platform evolves to require:

### When to Consider AKS

| Requirement | ACA Limitation | AKS Solution |
|---|---|---|
| **Namespace-level tenant isolation** | All apps share the same ACA environment | Dedicated namespaces per tenant with ResourceQuotas |
| **Custom network policies** | Managed VNet, limited egress control | Calico network policies, custom egress via Azure Firewall |
| **Service mesh (Istio/Linkerd)** | Basic traffic splitting only | Advanced routing, mTLS, circuit breaking, fault injection |
| **Multi-cloud portability** | Azure-only | Same K8s manifests run on EKS, GKE, on-prem |
| **Existing K8s ecosystem** | Separate infra from company standard | Integrate with existing ArgoCD, cert-manager, Prometheus stacks |
| **GPU workloads** | Not supported in ACA | AKS GPU node pools for ML-based fraud detection on invoices |

### Migration Path: ACA → AKS

The migration is straightforward because **both run the same Docker containers**. The domain code, EF Core, MassTransit, and OpenTelemetry all remain unchanged.

**Step 1: Generate K8s manifests from Aspire**

Use **[Aspirate](https://github.com/prom3theu5/aspirational-manifests)** (a community tool endorsed by Microsoft) to read `AppHost.cs` and generate Kubernetes YAML:

```bash
aspirate generate
```

This produces `deployment.yaml`, `service.yaml`, `configmap.yaml`, and `ingress.yaml` — mapping Aspire's internal references to Kubernetes ClusterIP services and injecting connection strings via ConfigMap/Secrets.

**Step 2: Replace infra provisioning**

Switch from `azd`/Bicep for ACA to **Terraform** (or Bicep for AKS) to provision:
- AKS cluster with appropriate node pools
- VNet with subnets for AKS nodes and private endpoints
- Application Gateway Ingress Controller (AGIC) instead of ACA managed ingress

**Step 3: Adopt GitOps**

Replace `azd deploy` in CI/CD with:
- **ArgoCD** or **Flux**: Watches Git repo for K8s manifest changes → automatically syncs to AKS.
- **Helm**: Package K8s manifests for templating and versioning.
- **Kustomize**: Environment-specific overlays (dev/staging/prod) over base manifests.

**Step 4: What changes vs. what stays**

| Component | Stays the Same | Changes |
|---|---|---|
| **API code** | All domain logic, handlers, validators | None |
| **EF Core** | DbContext, entity configs, migrations | Connection string sourced from K8s Secret instead of ACA binding |
| **MassTransit** | Outbox pattern, consumers, sagas | Service Bus connection via K8s Secret |
| **OpenTelemetry** | Same ServiceDefaults config | Exporters may route to Prometheus/Grafana instead of just App Insights |
| **Infrastructure** | N/A — rewritten | Bicep for ACA → Terraform for AKS + VNet + AGIC |
| **Deployment** | N/A — rewritten | `azd deploy` → `kubectl apply` or ArgoCD sync |
| **Ingress** | N/A — rewritten | ACA managed ingress → NGINX Ingress or AGIC |
| **Secrets** | Key Vault remains the source of truth | ACA Managed Identity binding → K8s Secrets via CSI driver or External Secrets Operator |
| **Scaling** | Same metrics triggers | ACA autoscaling rules → K8s HPA + KEDA |
| **CI/CD** | Same build + test stages | Deployment stage changes to `kubectl` / Helm / ArgoCD |

### The key insight: ACA is AKS with a managed control plane. When you outgrow the managed convenience, you graduate to the raw platform — and your application doesn't notice the difference.
