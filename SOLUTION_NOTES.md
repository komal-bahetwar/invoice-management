# SOLUTION_NOTES.md — Invoice Management API

## 1. How to Run the Project

See [README.md](./README.md) for full instructions. Quick start:

```bash
dotnet run --project backend/src/InvoiceManagement.AppHost
```

## 2. Assumptions

- **Tenant identification**: Via `X-Tenant-Id` HTTP header (Finbuckle header strategy)
- **Invoice numbering**: Auto-generated format `INV-{year}-{sequence:D6}`, unique per tenant
- **Currency**: Defaults to "USD" (ISO 4217), per-invoice override supported
- **Tax**: Simple percentage rate applied to subtotal; no multi-rate/composite tax
- **Status lifecycle**: Draft → Sent → (Paid | Overdue → Paid | Cancelled); Paid and Cancelled are terminal
- **Overdue detection**: System-driven, set-based operation; not in the 5 endpoint scope but modeled in domain
- **Authentication**: Duende IdentityServer scaffolded but not fully implemented (JWT validation placeholder)
- **Tenant provisioning**: In-memory store for dev; production would use a database-backed tenant store
- **Single currency per invoice**: No multi-currency line items
- **No payment gateway integration**: Status updates are manual/simulated
- **No email notifications**: Domain events are published via MassTransit Outbox; consumers are future scope
- **Sequential invoice numbers**: Numbered per year, per tenant; concurrency handled at persistence layer

## 3. Architecture Overview

**Modular Monolith + Clean Architecture + CQRS**

```
┌──────────────────────────────────────────────┐
│  API Host (InvoiceManagement.Api)            │
│  - Thin composition root                     │
│  - Middleware: Exception, Tenant, Serilog     │
├──────────────────────────────────────────────┤
│  Invoicing Module                            │
│  ┌─────────────┬──────────────┬─────────────┐│
│  │  API        │  Application │  Infra      ││
│  │  Controller │  Cmd/Query   │  EF Core    ││
│  │             │  Validators  │  Repository ││
│  │             │  Behaviors   │  Migrations ││
│  └─────────────┴──────────────┴─────────────┘│
│  ┌──────────────────────────────────────────┐│
│  │  Domain                                  ││
│  │  Invoice (AR), InvoiceLineItem (Entity)  ││
│  │  InvoiceStatus (Enum), InvoiceNumber (VO)││
│  │  Domain Events, Repository Interfaces   ││
│  └──────────────────────────────────────────┘│
└──────────────────────────────────────────────┘
```

### Why Modular Monolith

| Factor | Rationale |
|---|---|
| Deployment simplicity | Single deployable; no distributed system overhead for assessment scope |
| Module boundaries | Clean contracts; extraction path to microservices if needed |
| Assessment timebox | Fits 5–6 hours; demonstrates production awareness without over-engineering |

### CQRS Approach

- **Commands** (CreateInvoice, UpdateInvoiceStatus): Change state → write to DB → publish domain events
- **Queries** (GetInvoice, ListInvoices, GetDashboard): Read state → no side effects
- Logical separation only; no separate read/write databases for this scope

## 4. Domain Model

### Invoice Aggregate (Aggregate Root)

| Field | Type | Notes |
|---|---|---|
| Id | `Guid` | PK, generated on creation |
| InvoiceNumber | `InvoiceNumber` (VO) | `INV-{year}-{sequence}`, unique per tenant |
| CustomerName | `string` | Required, max 255 |
| CustomerEmail | `string` | Required, valid email, max 255 |
| CustomerAddress | `string?` | Optional, max 500 |
| IssueDate | `DateTimeOffset` | Required |
| DueDate | `DateTimeOffset` | Required, ≥ IssueDate |
| Status | `InvoiceStatus` | Enum, lifecycle-governed |
| SubTotal | `decimal` | Sum of line item totals |
| TaxRate | `decimal` | 0–100% |
| TaxAmount | `decimal` | SubTotal × (TaxRate / 100) |
| TotalAmount | `decimal` | SubTotal + TaxAmount |
| Currency | `string` | ISO 4217, default "USD" |
| Notes | `string?` | Optional, max 2000 |
| RowVersion | `byte[]` | Optimistic concurrency |

### InvoiceLineItem (Child Entity)

| Field | Type | Notes |
|---|---|---|
| Id | `Guid` | PK |
| Description | `string` | Required, max 500 |
| Quantity | `int` | > 0 |
| UnitPrice | `decimal` | > 0 |
| TotalPrice | `decimal` | Quantity × UnitPrice |

### InvoiceStatus Lifecycle

```
Draft ──→ Sent ──→ Paid (terminal)
          │   └──→ Overdue ──→ Paid (terminal)
          └──→ Cancelled (terminal)
Draft ──→ Cancelled (terminal)
```

**Allowed transitions:**

| From | To | Rule |
|---|---|---|
| Draft | Sent | Default flow |
| Sent | Paid | Payment received |
| Sent | Overdue | System-detected: DueDate < Now |
| Sent | Cancelled | Manual cancellation |
| Draft | Cancelled | Cancelled before sending |
| Overdue | Paid | Late payment |
| Paid | *any* | **BLOCKED** — terminal |
| Cancelled | *any* | **BLOCKED** — terminal |

## 5. Database Design

### Schema Strategy

**Schema-per-tenant** via Finbuckle.MultiTenant:
- Single SQL Server database
- Each tenant gets its own schema: `tenant_{sanitized-id}`
- Finbuckle auto-routes EF Core to the correct schema per request
- Max isolation without per-tenant database overhead

### Tables

```
tenant_{id}.Invoices
├── Id (PK, GUID)
├── InvoiceNumber (NVARCHAR(50), UNIQUE)
├── CustomerName (NVARCHAR(255))
├── CustomerEmail (NVARCHAR(255))
├── CustomerAddress (NVARCHAR(500), NULL)
├── IssueDate (DATETIMEOFFSET)
├── DueDate (DATETIMEOFFSET)
├── Status (INT)
├── SubTotal (DECIMAL(18,2))
├── TaxRate (DECIMAL(5,2))
├── TaxAmount (DECIMAL(18,2))
├── TotalAmount (DECIMAL(18,2))
├── Currency (NVARCHAR(3))
├── Notes (NVARCHAR(2000), NULL)
├── CreatedAt (DATETIMEOFFSET)
├── UpdatedAt (DATETIMEOFFSET)
└── RowVersion (ROWVERSION)

tenant_{id}.InvoiceLineItems
├── Id (PK, GUID)
├── InvoiceId (FK → Invoices.Id, CASCADE DELETE)
├── Description (NVARCHAR(500))
├── Quantity (INT)
├── UnitPrice (DECIMAL(18,2))
├── TotalPrice (DECIMAL(18,2))
├── CreatedAt (DATETIMEOFFSET)
└── UpdatedAt (DATETIMEOFFSET)
```

### Indexing Strategy

| Table | Index | Type | Purpose |
|---|---|---|---|
| Invoices | `InvoiceNumber` | Unique, non-clustered | Invoice number lookups |
| Invoices | `Status` | Non-clustered | Filter by status, dashboard |
| Invoices | `DueDate` | Non-clustered | Overdue detection |
| Invoices | `IssueDate` | Non-clustered | Date-range filtering |
| Invoices | `(Status, DueDate)` | Composite | Overdue invoice queries |
| InvoiceLineItems | `InvoiceId` | Non-clustered (FK) | Invoice → line items join |

## 6. API Design

### Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/invoices` | Bearer JWT | Create invoice (Draft) |
| `GET` | `/api/invoices` | Bearer JWT | List invoices (paginated) |
| `GET` | `/api/invoices/{id}` | Bearer JWT | View invoice details |
| `PATCH` | `/api/invoices/{id}/status` | Bearer JWT | Update status |
| `GET` | `/api/invoices/dashboard` | Bearer JWT | Dashboard summary |

### Response Envelope

All responses use `ApiResponse<T>`:
```json
{ "success": true, "data": { ... }, "errors": [] }
```

### Pagination
```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 150, "totalPages": 8 }
```

### Query Parameters for List

| Param | Type | Description |
|---|---|---|
| `page` | int | Page number (default 1) |
| `pageSize` | int | Items per page (1-100, default 20) |
| `status` | string | Filter: draft/sent/paid/overdue/cancelled |
| `search` | string | Search customer name, email, invoice number |
| `fromDate` | DateTimeOffset | Filter by issue date start |
| `toDate` | DateTimeOffset | Filter by issue date end |

### Error Responses

- **400** — Validation errors (ProblemDetails RFC 7807)
- **404** — Not found
- **409** — Conflict (invalid status transition)
- **500** — Internal server error

## 7. Validation Approach

- **Input validation**: FluentValidation on all commands via MediatR pipeline behavior
- **Domain validation**: Business rules enforced by the `Invoice` aggregate (e.g., status lifecycle)
- **Database constraints**: Unique indexes, FK constraints, precision/scale on monetary columns

See `CreateInvoiceCommandValidator.cs` and `UpdateInvoiceStatusCommandValidator.cs` for the full validation rules.

## 8. Tenant Isolation Approach

**Finbuckle.MultiTenant** with schema-per-tenant strategy:

1. **Resolution**: `X-Tenant-Id` header on every request → Finbuckle middleware resolves `TenantInfo`
2. **Data isolation**: Each tenant's data lives in its own SQL Server schema (`tenant_{id}`)
3. **Automatic routing**: EF Core's `MultiTenantDbContext` auto-applies schema prefix to all queries
4. **No cross-tenant queries**: Each request is scoped to exactly one tenant
5. **Dev experience**: In-memory tenant store with seeded `dev-tenant` for local development

## 9. Indexing and Performance Strategy

- **Hot path (List)**: Composite index on `(Status, DueDate)` for filtered + sorted queries
- **Hot path (Dashboard)**: Status index for aggregation queries
- **Uniqueness**: Unique constraint on `InvoiceNumber` prevents duplicates
- **Concurrency**: `RowVersion` column for optimistic concurrency on status updates
- **Pagination**: Offset-based with `Skip/Take`; capped at pageSize 100 to prevent abuse
- **Future**: Consider keyset pagination for large datasets; read-optimized dashboard projections

## 10. Testing Approach

### Layers Tested

| Layer | Type | Tools | What's Tested |
|---|---|---|---|
| Domain | Unit | xUnit + Shouldly | Entity creation, status transitions, invariants |
| Application | Unit | xUnit + NSubstitute | Command/Query handlers with mocked dependencies |
| Common | Unit | xUnit + Shouldly | Money value object arithmetic and validation |
| Architecture | Unit | NetArchTest | Layer dependency rules, naming conventions |
| API | Integration | WebApplicationFactory | HTTP contracts, error responses, pagination |

### What's Not Tested (Assessment Scope)

- Full integration tests with SQL Server Testcontainers (scaffolded but not exhaustive)
- Overdue detection logic (background process, out of core 5 endpoints)
- MassTransit Outbox event publishing
- Tenant isolation at the infrastructure level

## 11. Azure Deployment and Monitoring Considerations

### Services

| Azure Service | Purpose |
|---|---|
| **Azure App Service** | Host the ASP.NET Core API (Linux, B1/B2 tier) |
| **Azure SQL Database** | Managed SQL Server with geo-replication |
| **Azure Service Bus** | MassTransit transport for domain events (replace in-memory dev transport) |
| **Azure Container Registry** | Store Docker images for CI/CD |
| **Azure Key Vault** | Connection strings, secrets, API keys |
| **Application Insights** | OpenTelemetry traces, metrics, performance monitoring |
| **Azure Monitor** | Log aggregation, alerting |

### Deployment Strategy

- **CI/CD**: GitHub Actions → build, test, publish Docker image → deploy to App Service deployment slot
- **Slots**: Staging slot for zero-downtime deployment + smoke tests → swap to production
- **Rollback**: Re-swap slots (instant rollback)
- **Migrations**: Run by Migrator container as a pre-deployment step; idempotent EF Core migrations

### Scaling

- **Horizontal**: Scale App Service instances based on CPU/memory metrics
- **Database**: Azure SQL elastic pool for multi-tenant cost efficiency
- **Caching**: Add Redis cache layer for dashboard projections when tenant count grows
- **Tenant onboarding**: Automate schema provisioning as part of tenant registration

### Monitoring

- **Health checks**: `/health` endpoint for load balancer probes
- **Structured logging**: Serilog → Application Insights for queryable logs
- **Distributed tracing**: OpenTelemetry spans across API → DB → Service Bus
- **Alerting**: Failed requests > threshold, DB DTU > 80%, overdue detection failures

## 12. Security Considerations

- **Tenant isolation**: Schema-per-tenant prevents cross-tenant data access at the database level
- **JWT Authentication**: Duende IdentityServer issues tokens with tenant claims; API validates on every request
- **Input validation**: All inputs validated server-side (never trust client)
- **HTTPS**: Enforced in production
- **RowVersion**: Prevents lost updates via optimistic concurrency
- **No SQL injection**: EF Core parameterized queries throughout
- **Least privilege**: App Service connects to SQL with limited permissions (no DDL at runtime; migrator handles DDL)

## 13. Known Limitations

1. **Dashboard is in-memory aggregation**: For large tenants, this should be a pre-computed projection refreshed on domain events
2. **Overdue detection is not implemented**: Background job (Hangfire/Azure Function) would scan `DueDate < Now AND Status = Sent`
3. **Invoice number generation race**: Current approach (count + 1) has a TOCTOU race under concurrent creates; production would use a sequence table or `HiLo` pattern
4. **No payment integration**: Status transitions are manual; production would integrate with a payment gateway
5. **No email notifications**: Domain events are published but no consumer exists for sending emails
6. **No pagination cursor**: Offset pagination can drift with concurrent inserts; keyset pagination would be better
7. **Tenant store is in-memory**: Production needs a persistent tenant store with provisioning workflow
8. **Auth is scaffolded**: No actual IdentityServer configuration, user management, or RBAC
9. **Single module**: Only the Invoicing module exists; a real platform would have many modules
10. **No caching**: Dashboard queries hit the database directly; Redis cache would reduce load

## 14. What I Would Improve with More Time

1. **Overdue detection background service**: Hangfire recurring job with set-based `UPDATE`
2. **Proper dashboard projections**: Materialized view or pre-aggregated table refreshed by domain event handlers
3. **HiLo invoice number generation**: Prevent TOCTOU race condition at scale
4. **Full IdentityServer integration**: User registration, login, JWT with tenant claims, RBAC
5. **Redis caching**: Cache dashboard and hot-queries with cache invalidation on domain events
6. **Keyset pagination**: Stable pagination for high-throughput list endpoints
7. **Tenant provisioning API**: Automated schema creation and connection string management
8. **MassTransit consumers**: Notification service, audit trail service
9. **DataDog/Prometheus metrics**: Business KPIs (invoices created/minute, average time-to-pay)
10. **Feature flags**: Gradual rollout of new statuses or business rules without redeployment
11. **API versioning**: URL-based or header-based versioning for backward compatibility
12. **Correlation ID propagation**: End-to-end across HTTP → MediatR → EF Core → Service Bus
