# ADR-002: Multi-Tenancy Strategy with Finbuckle

| Field | Value |
|---|---|
| **ADR ID** | 002 |
| **Title** | Multi-Tenancy — Schema-Per-Tenant Isolation via Finbuckle |
| **Status** | Accepted |
| **Date** | 2026-07-23 |
| **Decision Maker** | Komal Nbahetwar |
| **Supersedes** | N/A |
| **Superseded By** | N/A |

---

## 1. Context

The Invoice Management API is a **SaaS platform** serving multiple tenant organisations. Each tenant's data must be fully isolated — no tenant should ever see another tenant's invoices, line items, or dashboard summaries.

The system must:

- Resolve tenant identity from every incoming HTTP request
- Route all database queries to the correct tenant's data
- Keep tenant resolution logic out of the Domain and Application layers (Clean Architecture)
- Allow future promotion of high-volume tenants to dedicated databases with zero application code changes
- Work in local development (in-memory store) and production (SQL Server-backed store)

---

## 2. Decision

**We chose Finbuckle.MultiTenant (v10.1.2) as the multi-tenancy framework**, using:

- **Schema-per-tenant** database isolation strategy
- **Header-based** tenant resolution (`X-Tenant-Id`)
- **In-memory tenant store** for development, SQL Server-backed for production
- **ITenantProvider** domain abstraction to decouple Application/Domain layers from Finbuckle
- **IMultiTenantContextAccessor** (non-generic) injected where infrastructure coupling is acceptable

### 2.1 Why Finbuckle?

| Rationale | Detail |
|---|---|
| **Native .NET integration** | First-class ASP.NET Core middleware, DI registration, and `HttpContext` integration. No custom plumbing. |
| **EF Core awareness** | `MultiTenantDbContext` base class automatically scopes queries to tenant schemas. No manual `WHERE TenantId = @x` filters needed. |
| **Strategy system** | Resolution strategy (header, claim, host, etc.) and store (in-memory, EF Core, Configuration) are independently pluggable. |
| **Tenant-aware DI** | `WithPerTenantOptions<T>()` enables per-tenant configuration without conditional code. |
| **Production-proven** | Active open-source project with 1.5k+ GitHub stars, NuGet downloads in the millions, and regular releases. |
| **v10 maturity** | Significant API cleanup — `MultiTenantContext` constructor-based initialization, non-generic `IMultiTenantContextAccessor`, `AsyncLocal` context storage. |
| **Assessment fit** | Avoids building custom multi-tenancy infrastructure within a 5–6 hour timebox. |

---

## 3. Considered Alternatives

### 3.1 Custom Middleware + Global Query Filters

**Approach:** Write a custom ASP.NET Core middleware to parse `X-Tenant-Id`, store it in `HttpContext.Items`, and apply `HasQueryFilter(b => b.TenantId == currentTenantId)` globally in EF Core.

| ✅ Pros | ❌ Cons |
|---|---|
| Zero external dependencies | Must build tenant store, resolution, and caching from scratch |
| Full control over behaviour | EF Core global filters are fragile — forgetting `.IgnoreQueryFilters()` leaks data |
| | No per-tenant configuration support |
| | Schema-per-tenant impossible without deep EF Core model building customisation |

**Verdict:** Rejected. Reinventing multi-tenancy infrastructure is error-prone and not a good use of limited assessment time.

### 3.2 Database-per-Tenant from Day One

**Approach:** Provision a dedicated SQL Server database per tenant, with connection-string routing at the API level.

| ✅ Pros | ❌ Cons |
|---|---|
| Strongest possible isolation | High operational overhead — provisioning, migrations, monitoring per database |
| Independent scaling per tenant | Overkill for assessment scope (~dozens of tenants, not thousands) |
| No risk of cross-schema query bugs | Migration complexity — must apply migrations to N databases atomically |

**Verdict:** Rejected for the MVP. Schema-per-tenant gives equivalent data isolation with far lower operational cost. Finbuckle makes this an easy upgrade path later (see §7).

### 3.3 Row-Level Security (RLS) Only

**Approach:** Single shared schema with SQL Server Row-Level Security policies per tenant.

| ✅ Pros | ❌ Cons |
|---|---|
| Simplest schema management | No physical data separation |
| Single migration target | Risk of RLS policy misconfiguration leaking data |
| | Complicates backup/restore per tenant |
| | Schema-per-tenant is already easy with Finbuckle — no reason to settle for weaker isolation |

**Verdict:** Rejected as the primary strategy. RLS is applied as a secondary defence-in-depth layer (see §5.3).

---

## 4. Implementation Architecture

### 4.1 How It Works — Request Flow

```
Client Request
  │  GET /api/invoices
  │  Header: X-Tenant-Id: dev-tenant
  ▼
┌──────────────────────────────────────────────────────────┐
│  1. HTTP Middleware                                      │
│     app.UseMultiTenant()                                 │
│     Finbuckle reads X-Tenant-Id header                   │
│     Resolves TenantInfo from InMemoryStore               │
│     Sets IMultiTenantContext on AsyncLocal               │
└─────────────────────────┬────────────────────────────────┘
                          ▼
┌──────────────────────────────────────────────────────────┐
│  2. API Controller                                       │
│     InvoicesController receives request                  │
│     Sends MediatR command/query                          │
└─────────────────────────┬────────────────────────────────┘
                          ▼
┌──────────────────────────────────────────────────────────┐
│  3. Application Handler                                  │
│     CreateInvoiceCommandHandler                          │
│     Calls ITenantProvider.GetTenantId()                  │
│         (→ TenantProvider reads IMultiTenantContext)     │
│     Passes tenantId to Invoice.Create(...)               │
│     Invoice validates tenantId != Guid.Empty             │
└─────────────────────────┬────────────────────────────────┘
                          ▼
┌──────────────────────────────────────────────────────────┐
│  4. Infrastructure — EF Core                             │
│     InvoicingDbContext : MultiTenantDbContext            │
│     Finbuckle auto-scopes to schema: tenant_<id>         │
│     All queries transparently filtered                   │
└──────────────────────────────────────────────────────────┘
```

### 4.2 Key Components

#### Program.cs — Registration

```csharp
// Multi-Tenancy (Finbuckle)
builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant-Id")          // Resolution strategy
    .WithInMemoryStore(options =>               // Tenant store (dev)
    {
        options.Tenants.Add(new TenantInfo
        {
            Id = "a1b2c3d4-...",                // Internal GUID
            Identifier = "dev-tenant",          // Header value
            Name = "Development Tenant"
        });
    });

// Middleware must be in the pipeline
app.UseMultiTenant();
```

#### Domain Layer — ITenantProvider (Abstraction)

```csharp
// Domain/Interfaces/ITenantProvider.cs
// Zero Finbuckle dependency — pure domain abstraction
public interface ITenantProvider
{
    Guid GetTenantId();
}
```

#### Infrastructure Layer — TenantProvider (Implementation)

```csharp
// Infrastructure/Tenant/TenantProvider.cs
// Bridges Finbuckle's IMultiTenantContextAccessor → ITenantProvider
public sealed class TenantProvider : ITenantProvider
{
    private readonly IMultiTenantContextAccessor _accessor;

    public Guid GetTenantId()
    {
        var tenantInfo = _accessor.MultiTenantContext?.TenantInfo
            ?? throw new InvalidOperationException(
                "No tenant context available.");
        return Guid.Parse(tenantInfo.Id);
    }
}
```

#### EF Core — InvoicingDbContext

```csharp
// Infrastructure/Data/InvoicingDbContext.cs
// MultiTenantDbContext base class handles schema routing
public sealed class InvoicingDbContext : MultiTenantDbContext, IUnitOfWork
{
    public InvoicingDbContext(
        IMultiTenantContextAccessor accessor,   // Non-generic — v10 API
        DbContextOptions<InvoicingDbContext> options)
        : base(accessor, options) { }
}
```

#### DI Registration

```csharp
// Api/DependencyInjection.cs
services.AddScoped<
    Domain.Interfaces.ITenantProvider,
    Infrastructure.Tenant.TenantProvider>();
```

### 4.3 Tenant Identifier vs. Internal ID

| Field | Purpose | Example |
|---|---|---|
| `Identifier` | Public-facing; sent in `X-Tenant-Id` header | `"dev-tenant"` |
| `Id` | Internal UUID; stored in `Invoice.TenantId` | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| `Name` | Human-readable label | `"Development Tenant"` |

The separation of `Identifier` (friendly string) from `Id` (GUID) allows:
- Tenant slugs to be changed without affecting foreign keys in the database
- Clean domain validation — `Guid.Empty` is explicitly rejected

---

## 5. Security & Isolation Guarantees

### 5.1 Defence-in-Depth

| Layer | Isolation Mechanism |
|---|---|
| HTTP | `X-Tenant-Id` header required; tenant must exist in store |
| Application | `ITenantProvider.GetTenantId()` fails if context missing; `Invoice.Create()` rejects `Guid.Empty` |
| EF Core | `MultiTenantDbContext` scopes all queries to tenant schema |
| SQL Server | Row-Level Security applied as backup (future) |

### 5.2 What Cannot Happen

- **Tenant ID from request body** — never accepted; tenant identity comes only from the header, resolved by middleware before any handler runs.
- **Cross-schema queries** — EF Core context is bound to a single schema by Finbuckle at instantiation.
- **Null/empty tenant** — `Invoice.Create()` line 104–105 returns `Result.Failure("Tenant ID is required.")` if `Guid.Empty` is passed.

### 5.3 Integration Test Verification

Tests use a custom `TestMultiTenantContextAccessor` stub to simulate different tenants without needing actual HTTP requests:

```csharp
private sealed class TestMultiTenantContextAccessor : IMultiTenantContextAccessor
{
    public IMultiTenantContext? MultiTenantContext { get; set; }
}

var tenantInfo = new TenantInfo
{
    Id = "test-tenant-guid",
    Identifier = "test-tenant",
    Name = "Test Tenant"
};
var context = new MultiTenantContext<TenantInfo>(tenantInfo);
var accessor = new TestMultiTenantContextAccessor
    { MultiTenantContext = context };
```

---

## 6. Pros & Cons of Finbuckle

### ✅ Pros

| Area | Benefit |
|---|---|
| **Integration depth** | Works at middleware, DI, and EF Core levels simultaneously. Not just a header parser — it actively participates in the request pipeline. |
| **Clean Architecture compliance** | `ITenantProvider` abstraction means Domain/Application never reference Finbuckle. Only Infrastructure knows about it. |
| **Schema-per-tenant** | Physically separates tenant data. Each tenant gets `tenant_{id}.Invoices`, `tenant_{id}.InvoiceLineItems`, etc. |
| **Strategy pluggability** | Switch from header to JWT claim resolution by changing one line. Add a store backed by SQL Server without touching any other code. |
| **Future migration path** | Enterprise tenants can graduate to dedicated databases by updating their `TenantInfo.ConnectionString`. Finbuckle routes automatically. |
| **.NET 10 support** | v10.1.2 targets `net10.0` natively — no compatibility shims needed. |
| **Testability** | `IMultiTenantContextAccessor` is mockable/stubbable. Integration tests create fake contexts trivially. |

### ❌ Cons

| Area | Risk / Limitation | Mitigation |
|---|---|---|
| **External dependency** | Breaking changes between major versions (v9 → v10 changed constructor APIs, removed `SimpleMultiTenantContextAccessor`) | Pinned to exact version; integration tests catch regressions at build time |
| **Learning curve** | Understanding the interaction between `ITenantInfo`, `TenantInfo`, `IMultiTenantContext`, `IMultiTenantContextAccessor`, and `MultiTenantDbContext` requires reading Finbuckle source/docs | Encapsulated behind `ITenantProvider` — teams only interact with `GetTenantId()` |
| **Generic vs non-generic accessor confusion** | `AddMultiTenant<TenantInfo>()` registers `IMultiTenantContextAccessor` (non-generic) but not `IMultiTenantContextAccessor<ITenantInfo>`. Using the wrong type causes DI failures. | Documented in `TenantProvider` — always inject the non-generic `IMultiTenantContextAccessor` |
| **Schema migration complexity** | Each tenant schema must be migrated separately; `dotnet ef migrations script` must be applied to N schemas | Acceptable at current scale; a migration runner looping over tenant schemas is a future enhancement |
| **Vendor lock-in** | If Finbuckle is abandoned, the migration cost is significant (replacing `MultiTenantDbContext`, tenant resolution, DI wiring) | Finbuckle is open-source and the abstraction (`ITenantProvider`) protects the core domain from direct coupling |

---

## 7. Future Evolution

### 7.1 SQL Server Tenant Store (Production)

Replace `WithInMemoryStore(...)` with a SQL Server-backed store:

```csharp
builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant-Id")
    .WithEFCoreStore<TenantStoreDbContext, TenantInfo>();
```

### 7.2 Enterprise Tenant Promotion

To move a high-volume tenant to a dedicated database:

1. Back up and restore tenant schema to a new SQL Server instance
2. Update the tenant's `ConnectionString` in the tenant store
3. Restart the application (or use a refresh mechanism)

**No application code changes required.** Finbuckle's `MultiTenantDbContext` transparently routes based on the tenant's connection string.

### 7.3 JWT Claim Resolution

Switch from header to JWT claim for tenant resolution:

```csharp
builder.Services.AddMultiTenant<TenantInfo>()
    .WithClaimStrategy("tenant_id")
    .WithEFCoreStore<TenantStoreDbContext, TenantInfo>();
```

The rest of the system (`ITenantProvider`, `InvoicingDbContext`, handlers) remains unchanged.

---

## 8. References

- [Finbuckle.MultiTenant Documentation](https://www.finbuckle.com/MultiTenant)
- [Finbuckle GitHub Repository](https://github.com/Finbuckle/Finbuckle.MultiTenant)
- [ASP.NET Core Multi-Tenancy Overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/multi-tenancy)
- [Schema-Per-Tenant Pattern](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)
- [ADR-001: Solution Architecture](./architecture-decision-record.md#2-architecture-overview) — Modular Monolith + Clean Architecture
- [ADR-003: CQRS with MediatR](./architecture-decision-record.md#23-cqrs) — Command/Query separation
