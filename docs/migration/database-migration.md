# Database Migration Strategy

## Overview

The Invoice Management API uses **EF Core Code-First Migrations** targeting **SQL Server** with a **schema-per-tenant** isolation model via Finbuckle. This document covers how migrations are authored, applied in local development, and deployed to production.

---

## How Schema-Per-Tenant Affects Migrations

Each tenant gets its own SQL Server schema (e.g., `tenant_dev-tenant.invoices`, `tenant_acme-corp.invoices`). However, **schema creation is handled at runtime by Finbuckle** — migrations are written against a single logical schema and Finbuckle's `MultiTenantDbContext` transparently routes queries to the correct physical schema at runtime.

This means:
- You author **one migration** per model change, not one per tenant.
- At runtime, Finbuckle creates each tenant schema automatically when the first query hits it.
- Migrations apply to the **shared database**, Finbuckle handles the per-tenant routing.

---

## Local Development Workflow

### Prerequisites

- SQL Server running locally (via Docker, Aspire, or native install)
- Connection string in `appsettings.Development.json` (or Aspire service discovery)
- EF Core CLI tools installed: `dotnet tool install dotnet-ef` (run from `backend/`)

### Authoring a Migration

When you change the domain model (add/remove entities, change properties, add indexes, etc.):

```bash
# From backend/ directory
dotnet ef migrations add DescriptionOfChange \
    --project src/modules/invoicing/InvoiceManagement.Modules.Invoicing.Infrastructure
```

This uses `InvoicingDbContextFactory` (`Data/InvoicingDbContextFactory.cs`) which provides a design-time stub multi-tenant context — the full tenant resolution pipeline isn't needed for migration generation.

### Applying Migrations Locally

There are **two ways** to apply migrations in development:

#### Option 1: API auto-applies on startup (preferred during development)

The API applies pending migrations automatically on startup when running in `Development`:

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
{
    await app.UseDatabaseMigrationAsync();
}
```

`UseDatabaseMigrationAsync()` (`Extensions/DatabaseExtensions.cs`):
1. Checks for pending migrations via `dbContext.Database.GetPendingMigrationsAsync()`
2. If pending migrations exist → applies them via `dbContext.Database.MigrateAsync()`
3. If no pending migrations → calls `EnsureCreatedAsync()` as a fallback
4. Catches exceptions gracefully (e.g., if SQL Server isn't ready yet on first startup)

**Start everything via Aspire:**
```bash
dotnet run --project src/InvoiceManagement.AppHost
```

#### Option 2: Standalone Migrator

For manual control, run the migrator console app:

```bash
dotnet run --project src/InvoiceManagement.Migrator
```

The Migrator is identical to Option 1 but as a standalone process — useful for CI/CD pipelines and verifying migrations without starting the full API.

### Viewing Migrations

```bash
# List all applied and pending migrations
dotnet ef migrations list \
    --project src/modules/invoicing/InvoiceManagement.Modules.Invoicing.Infrastructure

# Generate SQL script (review before applying to production)
dotnet ef migrations script \
    --project src/modules/invoicing/InvoiceManagement.Modules.Invoicing.Infrastructure \
    --output migrations.sql
```

---

## Production Workflow

### Principle: API Never Runs Migrations

The `UseDatabaseMigrationAsync()` call is **gated behind `IsDevelopment()`**. In production, the API has zero responsibility for applying migrations — this eliminates the risk of:
- Multiple API instances racing to apply migrations simultaneously
- Migration failures causing API startup to hang or crash
- Elevated database permissions on the API's service account

### Standalone Migrator

The `InvoiceManagement.Migrator` project is the **only supported way** to apply migrations in production:

```bash
# Run as a Kubernetes Job, Docker one-shot container, or CI/CD step
dotnet run --project src/InvoiceManagement.Migrator \
    --ConnectionStrings:DefaultConnection="<production-connection-string>"
```

**Recommended deployment patterns:**

| Environment | Approach |
|---|---|
| **Kubernetes** | Run as an init-container or a one-shot Job before deploying the API pods |
| **Docker Compose** | Build a separate migrator image; run with `--rm` before starting the API service |
| **CI/CD Pipeline** | Add a migration step after build, before deploy — e.g., `dotnet run --project Migrator` against the target database |
| **Manual / VM** | Run the Migrator binary directly on the server before restarting the API process |

### Migration Order Guarantee

1. **Stop old API instances** (or deploy to a staging slot)
2. **Run the Migrator** — applies all pending migrations
3. **Start new API instances** — Finbuckle creates tenant schemas as requests arrive

---

## Multi-Tenant Schema Creation at Runtime

Migrations only handle the **shared physical tables** in the database. Per-tenant schemas are created automatically by Finbuckle's `MultiTenantDbContext` when the first request for a given tenant arrives.

**Sequence on first tenant request:**
1. `MultiTenantDbContext` checks if schema `tenant_dev-tenant` exists
2. If not, EF Core's `EnsureCreated()` creates tables in that schema based on the model snapshot
3. Subsequent requests reuse the existing schema

> **Schema naming convention:** Finbuckle auto-generates schema names as `tenant_{Identifier}` (e.g., `tenant_dev-tenant`). The `Identifier` comes from the tenant store configured in `Program.cs`. To customize schema names, create a custom `ITenantInfo` subclass with a `Schema` property.

### Adding New Tenants

When onboarding a new tenant, add it to the tenant store. The Finbuckle store source depends on environment:

| Environment | Store Type | Configuration |
|---|---|---|
| **Development** | In-Memory | `WithInMemoryStore(...)` in `Program.cs` |
| **Production** | EF Core / SQL Server (future) | `WithEFCoreStore<TenantStoreDbContext, TenantInfo>()` |

No additional migration steps are needed — the new tenant schema is created automatically on first access.

---

## Migration File Conventions

- One migration class per model change, named with a descriptive PascalCase suffix: `AddInvoiceTenantId`, `AddInvoiceDueDateIndex`, etc.
- Migrations live in `src/modules/invoicing/InvoiceManagement.Modules.Invoicing.Infrastructure/Migrations/`
- Never edit migration files by hand — use `dotnet ef migrations remove` and re-add if you need to adjust.
- Always review the generated SQL with `dotnet ef migrations script` before shipping to production.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `dotnet ef` not found | Run `dotnet tool install dotnet-ef` from the `backend/` directory |
| "No tenant context available" during `ef migrations add` | Ensure `InvoicingDbContextFactory` provides a valid stub tenant context |
| Migration applied but tables missing | Verify the tenant store has the expected tenants registered; Finbuckle creates schemas lazily |
| Schema name collision | Finbuckle uses `tenant_{Identifier}` — ensure `Identifier` values are unique |
| Integration tests fail after migration | Run the Migrator against the test database before running integration tests |
