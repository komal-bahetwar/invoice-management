# Architecture Decision Record — Invoice Management API

## Document Information

| Field | Value |
|---|---|
| Title | Invoice Management API — Architecture Decision Record |
| Document Type | Architecture Reference |
| Version | 1.0 |
| Last Updated | 2026-07-23 |
| Status | Draft (pre-implementation) |
| Author | Komal Nbahetwar |
| Context | Qwiik Technical Assessment — Hands-on Engineering Leader |

## Purpose

This document is the **single source of truth** for every architectural decision, technology choice, and design pattern used in the Invoice Management API. It serves as the primary reference for engineering, review, and onboarding purposes.

Every decision recorded here reflects an intentional trade-off made with production-minded judgment, not a default or assumption. Items listed under **Explicitly Avoided** must not be introduced without revisiting the relevant ADR.

---

## 1. Problem Statement

Build a **multi-tenant invoice management backend API** for a SaaS platform. The system must allow users within a tenant organization to:

1. Create an invoice
2. List invoices (paginated, filterable)
3. View invoice details
4. Update invoice status (with lifecycle enforcement)
5. Retrieve a basic invoice summary/dashboard

The assessment intentionally provides minimal field-level requirements to evaluate how the candidate thinks, designs, and makes trade-offs as an engineering leader.

---

## 2. Architecture Overview

### 2.1 High-Level Architecture

The system is implemented as a **modular monolith** following **Clean Architecture** and **CQRS** principles.

```
┌─────────────────────────────────────────────────────────┐
│                    API Host (Thin)                       │
│  InvoiceManagement.Api                                  │
│  - Middleware (Exception, Tenant Resolution)            │
│  - Composes modules via DI                              │
├─────────────────────────────────────────────────────────┤
│  Invoicing Module                                       │
│  ┌──────────────┬────────────────┬────────────────────┐ │
│  │   API Layer  │  Application   │  Infrastructure   │ │
│  │ Controllers  │  Commands      │  EF Core DbContext │ │
│  │              │  Queries       │  Repositories      │ │
│  │              │  Validators    │  Migrations        │ │
│  │              │  Behaviors     │  Outbox Tables     │ │
│  └──────────────┴────────────────┴────────────────────┘ │
│  ┌─────────────────────────────────────────────────────┐ │
│  │   Domain Layer                                      │ │
│  │   Entities, Value Objects, Domain Events, Enums     │ │
│  └─────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────┤
│  Shared Kernel                                          │
│  - BaseEntity, DomainEvent, Money Value Object          │
├─────────────────────────────────────────────────────────┤
│  Shared Infrastructure                                  │
│  - MassTransit, Finbuckle, Serilog extensions           │
└─────────────────────────────────────────────────────────┘
```

### 2.2 Why Modular Monolith?

| Factor | Rationale |
|---|---|
| Deployment simplicity | Single deployable unit; no distributed system overhead |
| Module boundaries | Clean contracts between modules; extraction path to microservices |
| Assessment scope | Fits 5–6 hour timebox; demonstrates production awareness without over-engineering |
| Future evolution | Modules can be extracted to services if scale or team boundaries require it |

### 2.3 CQRS

Commands and queries are separated at the application layer:

- **Commands** represent intent to change state (CreateInvoice, UpdateInvoiceStatus)
- **Queries** retrieve state without side effects (GetInvoice, ListInvoices, GetDashboard)
- Physical separation (separate read/write databases) is not applied — logical separation within the Invoicing module is sufficient for this scope

---

## 3. Technology Stack

### 3.1 Backend Platform

| Area | Choice | Version | Rationale |
|---|---|---|---|
| Runtime | .NET | 10 | Both reference tech-stack docs target latest .NET |
| Language | C# | 14 | Strong typing, domain modeling, long-term maintainability |
| API Framework | ASP.NET Core | 10 | Required by assessment; industry standard |
| ORM | Entity Framework Core | 10 | Required; rich domain mapping, migrations, Outbox support |
| Database | SQL Server | 2022 | Required by assessment; Docker container for local dev |

### 3.2 Architecture Patterns & Libraries

| Library | Purpose |
|---|---|
| MediatR | Command/query dispatch via mediator pattern; pipeline behaviors |
| FluentValidation | Input validation for commands; auto-validated via pipeline behavior |
| Mapster | Object mapping — domain entities to DTOs; preferred over AutoMapper |
| Finbuckle.MultiTenant | Tenant resolution, schema-per-tenant routing, tenant-aware DI |
| Scrutor | Assembly scanning for automatic DI registration |

### 3.3 Messaging & Integration

| Library | Purpose |
|---|---|
| MassTransit | Message bus abstraction; transactional Outbox, retry, dead-letter |
| Transport (Dev) | In-Memory — zero-setup local development |
| Transport (Prod) | Azure Service Bus — cloud-native, managed, geo-redundant |

### 3.4 Authentication & Identity

| Library | Purpose |
|---|---|
| Duende IdentityServer | 8.0+ — OAuth 2.0 / OpenID Connect identity provider |
| ASP.NET Core Auth | JWT Bearer token validation; tenant claims for authorization |

**Status:** Scaffolded for the assessment. Full identity implementation (user registration, client configuration, federation) is documented but not implemented.

### 3.5 Observability & Logging

| Library | Purpose |
|---|---|
| Serilog | Structured logging with enrichment (tenant, correlation IDs) |
| Seq | Local log visualization; searchable structured log viewer |
| OpenTelemetry | Distributed tracing and metrics (production) |

### 3.6 API Documentation

| Library | Purpose |
|---|---|
| Swashbuckle | OpenAPI 3.0 spec generation from XML comments and attributes |
| Scalar | Modern API documentation UI; replaces Swagger UI |

### 3.7 Local Development

| Tool | Purpose |
|---|---|
| .NET Aspire | Local orchestration — SQL Server container, Seq container, API host |
| Docker | SQL Server 2022 container managed by Aspire |

### 3.8 Resilience

| Library | Purpose |
|---|---|
| Polly | Retry policies, circuit breaker, timeout for external calls |

### 3.9 Testing

| Library | Purpose |
|---|---|
| xUnit | Unit and integration test framework |
| Shouldly | Readable, human-friendly assertions |
| NSubstitute | Mocking framework for isolated unit tests |
| Bogus | Realistic fake data generation for tests |
| Testcontainers | Real SQL Server instance for integration tests |
| Respawn | Fast database reset between integration test runs |
| WebApplicationFactory | In-memory API integration testing |
| NetArchTest | Architecture rule enforcement (layer dependencies, naming) |

### 3.10 Explicitly Avoided

| Library | Reason |
|---|---|
| AutoMapper | Hidden performance issues, magic mapping, hard to debug; Mapster preferred |
| NServiceBus | Commercial license; MassTransit provides equivalent reliability |
| Newtonsoft.Json | System.Text.Json is faster and built into .NET 10 |
| FluentAssertions | License concerns; Shouldly is sufficient |
| NLog | Serilog preferred for structured logging ecosystem |
| Elsa Workflows | Not justified for 5-status lifecycle; keep simple |
| Hangfire / Quartz.NET | No scheduled jobs in assessment scope |
| Redis | No caching requirements for this scope |

---

## 4. Domain Model

### 4.1 Invoice Aggregate

The `Invoice` is the aggregate root. It owns `InvoiceLineItem` entities and enforces all business invariants.

| Field | Type | Constraints |
|---|---|---|
| Id | `Guid` | Primary key, generated on creation |
| InvoiceNumber | `string` | Auto-generated: `INV-{year}-{sequence}`; unique per tenant |
| CustomerName | `string` | Required, max 255 chars |
| CustomerEmail | `string` | Required, valid email format, max 255 chars |
| CustomerAddress | `string?` | Optional, max 500 chars |
| IssueDate | `DateTimeOffset` | Required |
| DueDate | `DateTimeOffset` | Required; must be >= IssueDate |
| Status | `InvoiceStatus` | Enum; lifecycle-governed |
| LineItems | `IReadOnlyCollection<InvoiceLineItem>` | At least 1 required |
| SubTotal | `decimal` | Computed: sum of line item totals |
| TaxRate | `decimal` | 0–100; percentage |
| TaxAmount | `decimal` | Computed: SubTotal × (TaxRate / 100) |
| TotalAmount | `decimal` | Computed: SubTotal + TaxAmount |
| Currency | `string` | ISO 4217; default "USD" |
| Notes | `string?` | Optional, max 2000 chars |
| CreatedAt | `DateTimeOffset` | Set on creation; immutable |
| UpdatedAt | `DateTimeOffset` | Updated on every change |

### 4.2 InvoiceLineItem Entity

| Field | Type | Constraints |
|---|---|---|
| Id | `Guid` | Primary key |
| InvoiceId | `Guid` | FK to Invoice |
| Description | `string` | Required, max 500 chars |
| Quantity | `int` | > 0 |
| UnitPrice | `decimal` | > 0 |
| TotalPrice | `decimal` | Computed: Quantity × UnitPrice |

### 4.3 InvoiceStatus Lifecycle

```
                    ┌──────────┐
                    │  Draft   │
                    └────┬─────┘
                         │ Send
                         ▼
                    ┌──────────┐
               ┌───│   Sent   │───┐
               │   └────┬─────┘   │
               │ Mark    │        │ Cancel
               │ Overdue │ Pay    │
               ▼         ▼        ▼
         ┌──────────┐ ┌──────┐ ┌───────────┐
         │ Overdue  │ │ Paid │ │ Cancelled │
         └────┬─────┘ └──────┘ └───────────┘
              │ Pay
              ▼
         ┌──────┐
         │ Paid │
         └──────┘
```

| Status | Editable? | Allowed Transitions | Terminal? |
|---|---|---|---|
| Draft | Yes | → Sent | No |
| Sent | No | → Paid, → Overdue, → Cancelled | No |
| Paid | No | None | **Yes** |
| Overdue | No | → Paid | No |
| Cancelled | No | None | **Yes** |

Overdue is system-detected: a background check identifies Sent invoices where `DueDate < UtcNow`.

### 4.4 Domain Events

Domain events are raised by aggregate methods and dispatched via MediatR notifications:

| Event | Trigger | Consumers |
|---|---|---|
| `InvoiceCreatedDomainEvent` | Invoice constructor | Audit logging, dashboard projection refresh |
| `InvoiceStatusChangedDomainEvent` | `MarkAsSent()`, `MarkAsPaid()`, `MarkAsCancelled()`, `MarkAsOverdue()` | Dashboard projections, notification service (future), audit trail |
| `InvoiceOverdueDetectedDomainEvent` | Background overdue detection | Notification service (future), audit trail |

---

## 5. API Design

### 5.1 Endpoints

| Method | Route | Description | Auth |
|---|---|---|---|
| `POST` | `/api/invoices` | Create a new invoice | Bearer JWT |
| `GET` | `/api/invoices` | List invoices (paginated, filterable) | Bearer JWT |
| `GET` | `/api/invoices/{id}` | View invoice details | Bearer JWT |
| `PATCH` | `/api/invoices/{id}/status` | Update invoice status | Bearer JWT |
| `GET` | `/api/invoices/dashboard` | Invoice summary/dashboard | Bearer JWT |

### 5.2 Request/Response Patterns

- **Envelope:** All responses wrapped in `ApiResponse<T>`:
  ```json
  { "success": true, "data": { ... }, "errors": [] }
  ```
- **Pagination:** `PagedResponse<T>` with offset-based pagination:
  ```json
  { "items": [...], "page": 1, "pageSize": 20, "totalCount": 150, "totalPages": 8 }
  ```
- **Tenant resolution:** `X-Tenant-Id` header on every request
- **Validation errors:** HTTP `400` with `ProblemDetails` (RFC 7807)
- **Not found:** HTTP `404` with `ProblemDetails`
- **Conflict:** HTTP `409` for invalid status transitions or duplicate invoice numbers

### 5.3 Dashboard Response

```json
{
  "totalInvoices": 150,
  "totalAmount": 245000.00,
  "byStatus": {
    "draft": 10,
    "sent": 45,
    "paid": 80,
    "overdue": 15,
    "cancelled": 0
  },
  "overdueAmount": 32000.00,
  "paidThisMonth": 85000.00,
  "currency": "USD"
}
```

### 5.4 Create Invoice Request

```json
{
  "customerName": "Acme Corp",
  "customerEmail": "billing@acme.com",
  "customerAddress": "123 Main St, Springfield, IL 62701",
  "issueDate": "2026-07-23T00:00:00Z",
  "dueDate": "2026-08-22T00:00:00Z",
  "taxRate": 8.5,
  "currency": "USD",
  "notes": "Net 30 terms",
  "lineItems": [
    { "description": "Consulting - July 2026", "quantity": 40, "unitPrice": 150.00 },
    { "description": "Cloud Infrastructure", "quantity": 1, "unitPrice": 2500.00 }
  ]
}
```

### 5.5 Update Status Request

```json
{
  "status": "Sent"
}
```

---

## 6. Database Design

### 6.1 Schema Strategy

- **Shared database, separate SQL Server schema per tenant**
- Schema naming: `tenant_{sanitized-tenant-id}`
- Each schema contains identical table structure (one set of tables per tenant)
- Managed by **Finbuckle.MultiTenant** — automatically routes EF Core context to correct schema

### 6.2 Tables (per tenant schema)

```
tenant_{id}.Invoices
├── Id (PK, GUID)
├── InvoiceNumber (NVARCHAR, unique index)
├── CustomerName
├── CustomerEmail
├── CustomerAddress
├── IssueDate
├── DueDate
├── Status (INT — enum mapped)
├── SubTotal (DECIMAL)
├── TaxRate (DECIMAL)
├── TaxAmount (DECIMAL)
├── TotalAmount (DECIMAL)
├── Currency (NVARCHAR)
├── Notes
├── CreatedAt (DATETIMEOFFSET)
└── UpdatedAt (DATETIMEOFFSET)

tenant_{id}.InvoiceLineItems
├── Id (PK, GUID)
├── InvoiceId (FK → Invoices.Id, CASCADE DELETE)
├── Description
├── Quantity (INT)
├── UnitPrice (DECIMAL)
└── TotalPrice (DECIMAL)

tenant_{id}.__Outbox (MassTransit Outbox table)
├── MessageId
├── MessageType
├── SentAt
└── ...
```

### 6.3 Indexing Strategy

| Table | Index | Type | Purpose |
|---|---|---|---|
| Invoices | `InvoiceNumber` | Unique, non-clustered | Lookup by invoice number |
| Invoices | `Status` | Non-clustered | Filter by status; dashboard queries |
| Invoices | `DueDate` | Non-clustered | Overdue detection; date-range filters |
| Invoices | `IssueDate` | Non-clustered | Date-range filters; reporting |
| Invoices | `(Status, DueDate)` | Composite, non-clustered | Dashboard: overdue invoices |
| LineItems | `InvoiceId` | Non-clustered (FK index) | Invoice → line items navigation |

### 6.4 Migration Strategy

- Migrations managed by standalone **InvoiceManagement.Migrator** console app
- API host never runs migrations — matches both tech-stack references
- Migrator runs as a pre-deployment step in CI/CD pipeline
- If migrations fail, API deployment does not proceed

---

## 7. Tenant Isolation

### 7.1 Isolation Approach

| Layer | Mechanism |
|---|---|
| HTTP | `X-Tenant-Id` header required on every request |
| Middleware | Finbuckle tenant resolution — validates tenant exists |
| EF Core | Schema-per-tenant — all queries scoped to `tenant_{id}` schema |
| Application | Tenant context flows through MediatR via `ITenantContext` |
| Database | Schema isolation + Row Level Security (defence-in-depth) |

### 7.2 Cross-Tenant Data Leak Prevention

- No cross-schema queries — EF Core context is schema-scoped
- Tenant ID never comes from request body — only from header
- Integration tests verify isolation by running requests with different `X-Tenant-Id` headers
- Row Level Security applied as defence-in-depth on SQL Server level

### 7.3 Enterprise Tenant Promotion (Future)

For high-volume tenants requiring dedicated resources:
- Migrate tenant schema to dedicated database
- Update Finbuckle tenant store to point to dedicated connection string
- No application code changes required — Finbuckle abstracts the routing

---

## 8. Validation Strategy

### 8.1 Two-Layer Validation

```
┌──────────────────────────────────────┐
│  Input Validation (FluentValidation) │
│  - Required fields                   │
│  - Format constraints (email, etc.)  │
│  - Range/numeric constraints          │
│  - Cross-field rules                 │
│  Return: 400 ProblemDetails          │
└──────────────┬───────────────────────┘
               ▼
┌──────────────────────────────────────┐
│  Business Rules (Domain + Handlers)  │
│  - Status transition validity        │
│  - Duplicate invoice number check    │
│  - At least 1 line item              │
│  - DueDate >= IssueDate              │
│  Return: 409 Conflict / 422          │
└──────────────────────────────────────┘
```

### 8.2 Validation Pipeline

`ValidationBehavior<TRequest, TResponse>` as MediatR pipeline behavior:
1. Intercepts every command/query before handler execution
2. Resolves `IValidator<TRequest>` from DI
3. Runs validation; if invalid, throws `ValidationException`
4. Global exception middleware catches it → returns `400 ProblemDetails`

### 8.3 Domain Invariants

Enforced in entity constructors and methods — cannot bypass even in unit tests:

- `Invoice` must have at least 1 `InvoiceLineItem`
- `DueDate` must be >= `IssueDate`
- `TaxRate` must be 0–100
- `SubTotal`, `TaxAmount`, `TotalAmount` are always recomputed when line items change
- Status transitions follow lifecycle; invalid transitions throw `DomainException`

---

## 9. Messaging & Event-Driven Architecture

### 9.1 Event Flow

```
Domain Entity raises DomainEvent
        │
        ▼
AggregateRoot dispatches via IDomainEventDispatcher
        │
        ▼
MediatR INotificationHandler handles domain event
        │
        ▼
Handler maps to IntegrationEvent + publishes via MassTransit
        │
        ▼
MassTransit stores in Outbox table (same DB transaction)
        │
        ▼
MassTransit Outbox processor delivers to transport
```

### 9.2 Transactional Outbox

- Domain event publication and database commit are **atomic** — both succeed or both fail
- MassTransit Outbox processor polls the outbox table and delivers messages asynchronously
- Eliminates dual-write problem (DB write succeeds but message publish fails)

### 9.3 Integration Events

| Event | Payload | Consumers |
|---|---|---|
| `InvoiceStatusChanged` | InvoiceId, OldStatus, NewStatus, TenantId, Timestamp | Dashboard projection refresh, audit log, notification service |
| `InvoiceOverdueDetected` | InvoiceId, DueDate, CustomerEmail, TenantId | Overdue notification, dashboard update |

---

## 10. Authentication & Identity

### 10.1 Architecture

- **Duende IdentityServer 8.0+** acts as the OAuth 2.0 / OpenID Connect provider
- APIs validate JWT Bearer tokens using ASP.NET Core authentication middleware
- Tenant context is embedded in JWT claims (`tenant_id`, `tenant_name`)
- Authorization is enforced at the handler level, not just controller level

### 10.2 Token Flow

```
Client ──Authorization Code (PKCE)──▶ IdentityServer
                                           │
                                           ▼
                                    Issues JWT with:
                                    - sub (user id)
                                    - tenant_id
                                    - tenant_name
                                    - scope
                                           │
                                           ▼
Client ──JWT Bearer + X-Tenant-Id──▶ API
                                           │
                                           ▼
                                    ASP.NET Core validates JWT
                                    Finbuckle resolves tenant from header
                                    Handler verifies tenant_id in JWT matches header
```

### 10.3 Assessment Scope

For the 5–6 hour assessment:
- Identity package references and configuration are **scaffolded**
- JWT validation is configured but uses development-only signing keys
- Full Duende IdentityServer setup (client registration, user store, federation) is documented but not implemented
- The `AI_USAGE.md` and `SOLUTION_NOTES.md` explicitly call this out as a known limitation

---

## 11. Project Structure

```
invoice-management/                              # Repository root
│
├── docs/                                        # All documentation
├── frontend/                                    # Frontend (out of scope for assessment)
│
└── backend/                                     # All backend code
    │
    ├── src/
    │   ├── common/
    │   │   ├── InvoiceManagement.Common.Domain/          # Shared kernel
    │   │   │   └── Shared/{BaseEntity, DomainEvent, Money}
    │   │   │
    │   │   └── InvoiceManagement.Common.Infrastructure/  # Cross-cutting infrastructure
    │   │       └── {Messaging, MultiTenancy, Logging} extensions
    │   │
    │   ├── modules/
    │   │   └── invoicing/                                # Invoicing module
    │   │       ├── Domain/     Entities, Enums, Events, Repositories (interfaces)
    │   │       ├── Application/ Commands, Queries, Handlers, DTOs, Validators, Contracts
    │   │       ├── Infrastructure/ EF Core DbContext, Repositories (impl), Migrations
    │   │       ├── Api/        Controllers
    │   │       │
    │   │       └── tests/                                # Module-specific tests
    │   │           ├── UnitTests/
    │   │           └── IntegrationTests/
    │   │
    │   ├── InvoiceManagement.Api/          Composition root (thin host)
    │   ├── InvoiceManagement.AppHost/      .NET Aspire orchestration
    │   ├── InvoiceManagement.Migrator/     Standalone DB migrator
    │   └── InvoiceManagement.sln
    │
    └── tests/                                        # Cross-cutting tests
        ├── InvoiceManagement.Api.IntegrationTests/
        ├── InvoiceManagement.ArchitectureTests/
        └── InvoiceManagement.Common.Tests/
```

---

## 12. .NET Aspire Local Orchestration

The `InvoiceManagement.AppHost` project orchestrates the local development environment:

```csharp
// Conceptual — InvoiceManagement.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlserver")
    .WithDataVolume();

var invoicingDb = sqlServer.AddDatabase("InvoicingDb");

var seq = builder.AddSeq("seq");

builder.AddProject<Projects.InvoiceManagement_Api>("api")
    .WithReference(invoicingDb)
    .WithReference(seq);

builder.Build().Run();
```

Single-command startup: `dotnet run --project backend/src/InvoiceManagement.AppHost`

Aspire dashboard shows: service health, logs, traces, environment variables, connection strings.

---

## 13. Azure Deployment

### 13.1 Target Services

| Component | Azure Service | Rationale |
|---|---|---|
| API Hosting | Azure Container Apps (ACA) | Serverless containers, auto-scaling, revision management |
| Database | Azure SQL Database | Managed SQL Server, geo-replication, point-in-time restore |
| Messaging | Azure Service Bus | Managed message broker, MassTransit-native integration |
| Identity | ACA-hosted IdentityServer or Azure AD B2C | Depends on identity requirements and licensing |
| Secrets | Azure Key Vault + Managed Identity | No secrets in code or config; runtime injection |
| Container Registry | Azure Container Registry (ACR) | Private, geo-replicated image storage |
| Observability | Application Insights + OpenTelemetry | Distributed tracing, metrics, live metrics, alerts |

### 13.2 Deployment Flow

```
Git Push → GitHub Actions
  ├── Build & Test (.NET build, xUnit tests, NetArchTest)
  ├── Build Docker image
  ├── Push image → ACR
  ├── Run Migrator (K8s Job / ACA Job)
  └── Deploy to ACA (revision-based, blue-green)
```

### 13.3 Scaling

- **ACA auto-scaling** based on HTTP request count, CPU, or memory
- **Azure SQL elastic pool** for multi-tenant database (cost-effective scaling)
- **Azure Service Bus** partitioned queues for high-throughput event processing
- **ACA revisions** for zero-downtime deployments with traffic splitting

### 13.4 Rollback Strategy

- ACA maintains revision history — rollback is reverting to previous active revision
- Database migrations are backward-compatible (additive only; no destructive changes)
- Traffic splitting allows canary deployments (10% → 50% → 100%)

---

## 14. Testing Strategy

### 14.1 Test Pyramid

```
        ┌──────┐
        │ E2E  │  (Not in assessment scope)
       ┌┴──────┴┐
       │  API   │  WebApplicationFactory, OpenAPI validation
      ┌┴────────┴┐
      │Integration│  Testcontainers (real SQL Server), Respawn
     ┌┴───────────┴┐
     │    Unit     │  xUnit, NSubstitute, Shouldly, Bogus
     └─────────────┘
     Architecture Tests: NetArchTest
```

### 14.2 What Each Layer Tests

| Layer | Tests | Key Scenarios |
|---|---|---|
| Domain Unit | `InvoiceTests.cs`, `MoneyTests.cs` | Status transitions, invariant enforcement, computed totals, domain event raising |
| Application Unit | `*CommandHandlerTests.cs`, `*ValidatorTests.cs` | Handler logic with mocked dependencies, validation rules |
| Infrastructure Integration | `InvoiceRepositoryTests.cs` | CRUD against real SQL Server, EF Core mappings, Outbox persistence |
| API Integration | `InvoicesControllerTests.cs` | HTTP request/response, error handling, pagination, tenant isolation |
| Architecture | `LayerDependencyTests.cs` | Domain has no EF/ASP.NET deps, module boundary rules, naming |

---

## 15. Security Considerations

| Concern | Approach |
|---|---|
| Tenant isolation | Schema-per-tenant via Finbuckle; X-Tenant-Id header; JWT tenant claim verification |
| Authentication | JWT Bearer tokens via Duende IdentityServer (scaffolded) |
| Authorization | Handler-level checks — tenant context must match JWT claims |
| Input validation | FluentValidation pipeline; never trust client input |
| SQL injection | Parameterized queries via EF Core (default) |
| Secrets | Key Vault + Managed Identity in production; .env (gitignored) in development |
| HTTPS | Enforced in production; development with .NET dev certs |
| Rate limiting | YARP or ACA built-in rate limiting in production |
| Audit trail | Domain events provide immutable, append-only audit log |

---

## 16. Assumptions & Trade-offs

| Decision | Trade-off |
|---|---|
| Modular monolith over microservices | Simpler deployment; modules can be extracted later if needed |
| Schema-per-tenant over discriminator column | Stronger isolation; higher operational complexity for schema management |
| CQRS (logical) over simple CRUD | Better separation of concerns; more boilerplate for simple reads |
| MassTransit + Outbox over fire-and-forget | Reliable event delivery; adds Outbox table and polling overhead |
| Scaffolded Identity over full implementation | Saves 2+ hours for assessment; demonstrates awareness without implementation |
| Mapster over AutoMapper | Faster, no magic, easier debugging; slightly less ecosystem familiarity |
| No Elsa Workflows | 5-status lifecycle doesn't justify workflow engine; keep simple |
| No Redis caching | Dashboard queries on small tenant datasets perform well without cache |

---

## 17. Known Limitations

1. **Authentication is not fully implemented** — IdentityServer is scaffolded; JWT validation uses dev-only keys
2. **No email notifications** — status change notifications require an email provider integration (SendGrid/SES)
3. **Single currency** — no multi-currency support or exchange rate handling
4. **No file attachments** — invoices cannot have PDF/image attachments
5. **Simple tax model** — flat tax rate per invoice; no multi-rate or jurisdiction-based tax
6. **Manual tenant provisioning** — tenants are added to an in-memory store; no tenant management API
7. **No workflow history** — status change history is captured via domain events but not exposed in API
8. **Dashboard is real-time** — in a high-volume system, dashboard would be a pre-computed projection

---

## 18. What Would Improve with More Time

1. **Full IdentityServer implementation** — user registration, login, client management, federation
2. **Pre-computed dashboard projections** — event-driven projection rebuilds for high-volume tenants
3. **Email notifications** — invoice status change emails via SendGrid or AWS SES
4. **PDF generation** — invoice PDF rendering on demand
5. **Multi-currency support** — exchange rates, currency conversion, multi-currency reporting
6. **Audit log API** — expose event history as an audit trail endpoint
7. **Tenant management API** — CRUD for tenant provisioning, configuration, feature flags
8. **Automated overdue detection** — scheduled background job (Quartz.NET) to mark overdue invoices
9. **Rate limiting and throttling** — per-tenant rate limits at API gateway level
10. **Terraform/Bicep IaC** — repeatable Azure infrastructure provisioning
11. **CI/CD pipeline** — GitHub Actions workflow for build, test, and deploy
12. **Load testing** — k6 or NBomber scripts to validate performance under tenant concurrency

---

## 19. References

- [Qwiik Technical Assessment Requirements](../requirement.md)
- [Tech Stack Reference v1](../tech-stack/tech-stack-v1.md) — SmartSavingsPlatform architecture
- [Tech Stack Reference v2](../tech-stack/tech-stack-v2.md) — Agentic Platform architecture
- [Implementation Plan](../../../.copilot/session-state/e5d98b34-cdfd-4cee-a3e3-1312ff54f795/plan.md) — Detailed implementation plan with todos
