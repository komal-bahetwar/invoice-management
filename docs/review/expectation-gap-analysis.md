# Expectation Gap Analysis — Invoice Management API

**Date:** 2026-07-23  
**Reference:** `docs/requirement.md` → `### Expectations`

This document cross-references the 10 reviewer expectations against the actual codebase implementation, identifying what's done well and what has gaps.

---

## 1. Good C# and ASP.NET Core Practices ✅

**Verdict: Strong**

| Area | Evidence |
|---|---|
| Clean Architecture | Strict 4-layer separation: Domain → Application → Infrastructure → Api. Enforced by NetArchTest (`LayerDependencyTests.cs`). |
| CQRS + MediatR | Every operation is a Command or Query with a dedicated Handler. Commands return `Result<T>`, Queries return DTOs. |
| Result pattern | `Result`/`Result<T>` from shared kernel used consistently across all layers — domain factories, handlers, controllers. |
| EF Core configs | `IEntityTypeConfiguration<T>` classes (`InvoiceConfiguration.cs`, `InvoiceLineItemConfiguration.cs`). No data annotations. |
| Object mapping | Mapster (`invoice.Adapt<InvoiceDto>()`) — not AutoMapper. |
| DI conventions | Scrutor assembly scanning auto-registers repositories by naming convention. |
| C# 14 features | Top-level statements, `sealed` classes, `required`/`init` properties, private EF Core constructors, primary constructors where appropriate. |
| Naming conventions | `{Action}{Entity}Command`/`Query`, handlers match request name + `Handler`, validators match + `Validator`. |

**Minor cosmetic issue:** `InvoicesController.cs` has a duplicate `using Microsoft.AspNetCore.Mvc;` import.

---

## 2. Sensible API Design ✅

**Verdict: Strong**

| Area | Evidence |
|---|---|
| RESTful endpoints | `POST /api/invoices`, `GET /api/invoices`, `GET /api/invoices/{id}`, `PATCH /api/invoices/{id}/status`, `GET /api/invoices/dashboard` |
| Response envelope | Consistent `ApiResponse<T>` with `Success`, `Data`, `Errors` array |
| Status codes | 201 (Created + Location header), 200, 400, 404, 409 (status conflict) |
| Pagination | `PagedResponse<T>` with `Page`, `PageSize`, `TotalCount`, `TotalPages` |
| Query params | Status filter, search term, date range (`fromDate`/`toDate`), page/pageSize |
| Abuse prevention | Page clamped to ≥1, pageSize clamped to 1-100 |
| OpenAPI | `MapOpenApi()` + Scalar API reference for interactive docs |

**Minor issues:**
- Duplicate `using Microsoft.AspNetCore.Mvc;` in `InvoicesController.cs`
- `using Microsoft.AspNetCore.Http;` is unused in the same file

---

## 3. Practical Database Modelling ✅

**Verdict: Strong**

| Area | Evidence |
|---|---|
| Aggregate design | `Invoice` as aggregate root, `InvoiceLineItem` as owned child entity with cascade delete |
| Monetary precision | `SubTotal(18,2)`, `TaxRate(5,2)`, `TaxAmount(18,2)`, `TotalAmount(18,2)` |
| Value object mapping | `InvoiceNumber` VO with EF `HasConversion` |
| Enum storage | `InvoiceStatus` stored as `int` with `HasConversion<int>()` |
| Optimistic concurrency | `RowVersion` as `IsRowVersion()` — tested with `DbUpdateConcurrencyException` |
| Tenant scoping | `TenantId` column on Invoice with index, `TenantEntity` base class |
| Migrations | Standalone `InvoiceManagement.Migrator` project; API never runs migrations in production |
| Indexing | `InvoiceNumber` (unique), `Status`, `DueDate`, `IssueDate`, composite `(Status, DueDate)`, `TenantId`, `InvoiceId` FK |

---

## 4. Multi-Tenant SaaS Awareness ⚠️

**Verdict: Partial — good isolation, but no auth, in-memory store only**

| Area | Evidence | Status |
|---|---|---|
| Tenant resolution | `X-Tenant-Id` header via Finbuckle header strategy | ✅ |
| Schema-per-tenant | `MultiTenantDbContext` base routes queries to tenant-specific schema | ✅ |
| Domain abstraction | `ITenantProvider` keeps domain decoupled from Finbuckle | ✅ |
| Tenant entity base | `TenantEntity` with `TenantId` property | ✅ |
| **Tenant authentication** | No JWT, no `[Authorize]`, tenant identity is trusted from header alone | ❌ |
| **Tenant store** | In-memory with one hardcoded `dev-tenant` — no provisioning workflow | ⚠️ |
| **Defense in depth** | No global query filter on `TenantId` — relies solely on schema isolation | ⚠️ |

**Note:** The `Program.cs` comment about schema naming (`// To customize schema names, create a custom ITenantInfo subclass`) suggests schema-per-tenant setup may be incomplete — the schema name defaults to `tenant_{Identifier}` which works for `dev-tenant` but may need customization for production tenant IDs.

---

## 5. SQL Server / EF Core Understanding ✅

**Verdict: Strong**

| Area | Evidence |
|---|---|
| Multi-tenant DbContext | Extends Finbuckle's `MultiTenantDbContext`, implements `IUnitOfWork` |
| Integration tests | `Testcontainers.MsSql` for real SQL Server testing |
| Concurrency testing | Simulated concurrent update via second `DbContext` → verifies `DbUpdateConcurrencyException` |
| Query composition | Repository builds `IQueryable` with conditional filters (deferred execution) |
| Eager loading | `.Include(i => i.LineItems)` on read queries |
| Migration tooling | `IDesignTimeDbContextFactory` for migration generation with design-time tenant |
| Standalone migrator | Separate console project handles `Database.MigrateAsync()` |

**Minor gap:** `ListAsync` uses `.ToLower()` in LINQ for search, which translates to `LOWER()` in SQL and prevents index usage. A case-insensitive collation or persisted computed column would be better at scale.

---

## 6. Validation and Error Handling ⚠️

**Verdict: Partial — strong input validation, but FluentValidation exceptions return 500**

| Area | Evidence | Status |
|---|---|---|
| Input validation | FluentValidation on all commands (`CreateInvoiceCommandValidator`, `UpdateInvoiceStatusCommandValidator`) | ✅ |
| Domain validation | `Invoice.Create()` factory validates name, email, dates, tax rate, currency, line items | ✅ |
| Status lifecycle | Aggregate methods enforce valid transitions (e.g., can't pay a Draft invoice) | ✅ |
| Global exception handler | `GlobalExceptionHandler` returns ProblemDetails (RFC 7807) for unhandled exceptions | ✅ |
| **FluentValidation → 500 bug** | `ValidationBehavior` throws `ValidationException` → caught by global handler → returns 500 instead of 400 | ❌ |
| **No FluentValidation handling** | `GlobalExceptionHandler` has no special case for `FluentValidation.ValidationException` | ❌ |

**Critical bug:** The `ValidationBehavior` (MediatR pipeline) throws `FluentValidation.ValidationException` when validators fail. This percolates to `GlobalExceptionHandler`, which returns a generic 500 Internal Server Error with `"An error occurred processing your request."`. All FluentValidation failures return 500 instead of 400.

The API integration test `CreateInvoice_InvalidRequest_ShouldReturnBadRequest` happens to pass only because the test's request triggers domain-level validation in the handler (`Result.Failure` return), not the pipeline behavior. A request that passes domain validation but fails FluentValidation would return 500.

**Location:** `ValidationBehavior.cs` line 37 → `GlobalExceptionHandler.cs` line 28

---

## 7. Query Efficiency and Pagination ✅

**Verdict: Strong, with well-documented limitations**

| Area | Evidence |
|---|---|
| Pagination | Offset-based `Skip/Take` with pageSize capped at 100 |
| Total count | `CountAsync` before pagination (separate query — tradeoff documented) |
| Index coverage | `InvoiceNumber` (unique), `Status`, `DueDate`, `IssueDate`, composite `(Status, DueDate)` |
| Eager loading | `.Include(i => i.LineItems)` on all invoice queries |
| Sorting | `OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.CreatedAt)` |
| Search | CustomerName, InvoiceNumber, CustomerEmail — `Contains` with `ToLower` |

**Documented limitations (in `SOLUTION_NOTES.md`):**
- Dashboard fetches all invoices (`pageSize = int.MaxValue`) and aggregates in memory
- Offset pagination can drift with concurrent inserts (keyset pagination noted as improvement)
- `.ToLower()` in LINQ prevents index usage for text search

---

## 8. Basic Security Thinking ❌

**Verdict: Missing — zero authentication implementation**

| Area | Evidence | Status |
|---|---|---|
| SQL injection | EF Core parameterized queries | ✅ |
| Lost update prevention | `RowVersion` optimistic concurrency | ✅ |
| Tenant isolation | Schema-per-tenant at DB level | ✅ |
| **Authentication** | No `AddAuthentication()`, no `[Authorize]`, no JWT middleware at all | ❌ |
| **Authorization** | No RBAC, no role claims, any tenant header accepted | ❌ |
| **Rate limiting** | No `AddRateLimiter()`, no throttling | ❌ |
| **CORS** | No CORS policy configured | ❌ |
| **HTTPS enforcement** | Not configured in code (mentioned in docs only) | ⚠️ |
| **Secret management** | Connection string with `sa` credentials in `appsettings.json` | ⚠️ |
| **Input sanitization** | Only FluentValidation — no anti-XSS or content security headers | ⚠️ |

**This is the biggest gap.** The `SOLUTION_NOTES.md` honestly lists "Auth is scaffolded" as limitation #8, but there is literally zero auth code. The `AGENTS.md` mentions "Duende IdentityServer 8.0+ (scaffolded)" in the tech stack, but no IdentityServer packages, configuration, or code exist in the project.

For a multi-tenant SaaS assessment that explicitly lists "Basic security thinking" as an expectation, this is a significant gap.

---

## 9. Testing of Important Business Rules ✅

**Verdict: Strong**

| Test Layer | File | Count | Coverage |
|---|---|---|---|
| **Domain unit tests** | `InvoiceTests.cs` | 12 | Entity creation, all status transitions (happy + failure), terminal state, full lifecycle |
| **Application unit tests** | `CreateInvoiceCommandHandlerTests.cs` | 3 | Valid creation, number collision, empty line items |
| **Application unit tests** | `UpdateInvoiceStatusCommandHandlerTests.cs` | 3 | Valid transition, not found, invalid transition |
| **Integration tests** | `InvoiceRepositoryTests.cs` | 6 | CRUD, pagination, status filter, number lookup, sequence count, concurrency conflict |
| **API tests** | `InvoicesApiTests.cs` | 5 | 201, 400, 404, paginated list, dashboard |
| **Architecture tests** | `LayerDependencyTests.cs` | 8 | Layer dependency rules, naming conventions |
| **Common tests** | `MoneyTests.cs` | 6 | Money creation, validation, arithmetic, rounding |

**Total: 43 tests across all layers.**

**Gaps:**
- No unit tests for `InvoiceLineItem.Create()` validation
- No unit tests for `InvoiceNumber` value object
- No tests for `TenantProvider`
- No tests for `ValidationBehavior` itself
- No test proving tenant isolation (tenant X shouldn't see tenant Y data)

---

## 10. Clear Documentation and Trade-off Explanation ✅

**Verdict: Strong**

| Document | Quality |
|---|---|
| `SOLUTION_NOTES.md` | Comprehensive — all 14 required sections, tables, ASCII diagrams, honest limitations |
| `AGENTS.md` | Excellent onboarding — commands, structure, code style, testing conventions |
| `README.md` | Quick start and project overview |
| `AI_USAGE.md` | AI usage disclosure |
| `docs/architecture/architecture-decision-record.md` | ADR documentation |
| `docs/requirement.md` | Assessment specification |

Trade-offs explicitly called out: Why Modular Monolith, Why CQRS (logical only), Why schema-per-tenant, Why not App Service/AKS, Why offset pagination.

---

## Summary

| # | Expectation | Verdict | Severity |
|---|---|---|---|
| 1 | Good C# and ASP.NET Core practices | ✅ Strong | — |
| 2 | Sensible API design | ✅ Strong | — |
| 3 | Practical database modelling | ✅ Strong | — |
| 4 | Multi-tenant SaaS awareness | ⚠️ Partial | Medium |
| 5 | SQL Server / EF Core understanding | ✅ Strong | — |
| 6 | Validation and error handling | ⚠️ Partial | High (bug) |
| 7 | Query efficiency and pagination | ✅ Strong | — |
| 8 | Basic security thinking | ❌ Missing | High |
| 9 | Testing of important business rules | ✅ Strong | — |
| 10 | Clear documentation and trade-off explanation | ✅ Strong | — |

## Top 3 Fixes Before Submission

See `docs/scratch/top-3-fixes-plan.md` for detailed implementation plan.

1. **Fix FluentValidation → 500 bug** (expectation #6) — 15 minutes
2. **Add authentication with JWT** (expectation #8) — 45 minutes
3. **Add rate limiting** (expectation #8) — 15 minutes