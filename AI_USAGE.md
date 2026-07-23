# AI_USAGE.md — Invoice Management API

## 1. Which AI Tools I Used

| Tool | Usage |
|---|---|
| **GitHub Copilot** | Primary coding assistant; used for file scaffolding, boilerplate generation, test writing, and documentation |
| **Claude (via Copilot)** | Used for the event storming workshop session (`docs/process/event-storming-session.md`) and for reviewing architectural decisions |

## 2. What I Used AI For

| Area | AI Contribution |
|---|---|
| **Project scaffolding** | Generated solution file, all 14 `.csproj` files with correct project references and package versions |
| **Domain layer** | Generated `BaseEntity`, `DomainEvent`, `Money`, `Result` shared kernel classes |
| **Entities** | Generated `Invoice` aggregate root and `InvoiceLineItem` entity with factory methods and status transition logic |
| **Domain events** | Generated `InvoiceCreatedDomainEvent`, `InvoiceStatusChangedDomainEvent`, `InvoiceOverdueDetectedDomainEvent` |
| **Application layer** | Generated commands, queries, handlers, validators, and the `ValidationBehavior` pipeline |
| **Infrastructure** | Generated `InvoicingDbContext`, entity configurations (`InvoiceConfiguration`, `InvoiceLineItemConfiguration`), and `InvoiceRepository` |
| **API layer** | Generated `InvoicesController` with all 5 endpoints, `GlobalExceptionHandler`, `DependencyInjection` module registration |
| **AppHost/Migrator** | Generated .NET Aspire `Program.cs` and standalone migrator |
| **Tests** | Generated unit tests for `Invoice`, `CreateInvoiceCommandHandler`, `UpdateInvoiceStatusCommandHandler`, `Money`; architecture tests; API integration test scaffold |
| **Documentation** | Generated initial `README.md`, `SOLUTION_NOTES.md`, this `AI_USAGE.md`, and the `event-storming-session.md` |
| **ADR content** | AI helped structure the Architecture Decision Record but all technical decisions were mine |
| **Gap analysis & fixes** | AI (Claude via Copilot) performed a comprehensive expectation gap analysis (`docs/review/expectation-gap-analysis.md`) and proposed an implementation plan (`docs/review/top-3-fixes-plan.md`) for the top 3 gaps |
| **Authentication (post-review fix)** | AI generated `AuthenticationExtensions.cs`, JWT config in `appsettings.Development.json`, and `AuthController` token endpoint |
| **Rate limiting (post-review fix)** | AI added `AddRateLimiter()` fixed-window policy and `[EnableRateLimiting]` to the controller |
| **Validation exception fix** | AI created `ValidationExceptionHandler.cs` to return 400 instead of 500 for FluentValidation errors |
| **Database startup fix** | AI corrected `DatabaseExtensions.cs` to always use `MigrateAsync()` instead of falling back to `EnsureCreatedAsync()` |
| **README updates** | AI updated README with auth section, security features, token endpoint docs, `.http` file instructions, and corrected solution file name |
| **`.http` test file** | AI updated `Invoices.http` with token generation flow and `Authorization` headers on all requests |

## 3. What I Personally Reviewed

- **Every file**: I reviewed all generated source code for correctness, consistency, and alignment with the ADR
- **Domain model**: I verified the `InvoiceStatus` lifecycle (Draft → Sent → Paid/Overdue/Cancelled), the allowed transitions, and the invariant enforcement
- **Invoice number format**: I chose `INV-{year}-{sequence:D6}` and verified the generation logic
- **Validation rules**: I reviewed all FluentValidation rules against the ADR specification
- **API shape**: I verified all 5 endpoints, response envelopes, pagination format, and error handling
- **Database design**: I reviewed entity configurations, index strategy, and schema-per-tenant setup
- **Test coverage**: I verified tests cover the critical business rules (status lifecycle, creation validation, monetary calculations)
- **Documentation accuracy**: I verified the architecture description, deployment considerations, and known limitations reflect the actual implementation

## 4. What AI Got Wrong (and How I Fixed It)

| Issue | AI Behavior | Fix |
|---|---|---|
| **Circular DI with Finbuckle store** | AI proposed `WithEFCoreStore<InvoicingDbContext, TenantInfo>()` which creates a circular dependency since the DbContext itself depends on `ITenantInfo` | Changed to `WithInMemoryStore()` for dev; noted in SOLUTION_NOTES that production needs a separate tenant store DbContext |
| **Missing IUnitOfWork on DbContext** | AI generated `InvoicingDbContext` without implementing `IUnitOfWork`, but DI registration expected it | Added `IUnitOfWork` interface to the DbContext class |
| **Namespace mismatches** | Several files had inconsistent namespace references between the Domain ValueObjects and the Infrastructure configuration | Fixed all namespace references to be consistent with project structure |
| **Pagination edge case** | Initial `ListAsync` didn't clamp `pageSize` to prevent abuse | Added `Math.Clamp(pageSize, 1, 100)` |
| **Missing module registration pattern** | AI initially put all DI configuration inline in `Program.cs` | Extracted to `AddInvoicingModule()` extension method for clean module composition |
| **Duplicate MediatR/Validation registrations** | AI registered MediatR and Validation both inline and via the module extension | Consolidated to single `AddInvoicingModule()` call |
| **`net10.0` TFM availability** | AI assumed `net10.0` is available (it's a preview as of July 2026) | Kept as-is since the tech stack reference docs specify .NET 10; noted in comments that the user may need preview SDK |
| **FluentValidation → 500 bug** | AI's `ValidationBehavior` threw `FluentValidation.ValidationException`, which the generic `GlobalExceptionHandler` caught and returned as 500 Internal Server Error instead of 400 | Created `ValidationExceptionHandler.cs` registered before `GlobalExceptionHandler` to catch `ValidationException` and return 400 with validation errors in ProblemDetails format |
| **`EnsureCreatedAsync` fallback** | AI's `DatabaseExtensions` fell back to `EnsureCreatedAsync()` when no pending migrations were found, which created tables from a stale model snapshot without the `TenantId` column | Changed to always call `MigrateAsync()` — `EnsureCreatedAsync()` is never safe when migrations exist |
| **Zero authentication code** | AI's `AGENTS.md` referenced "Duende IdentityServer 8.0+ (scaffolded)" in the tech stack but produced no authentication code whatsoever — no `[Authorize]`, no JWT middleware, no IdentityServer packages | Added JWT Bearer authentication with `AuthenticationExtensions`, `AuthController` for dev token generation, `[Authorize]` on controller, and `UseAuthentication()`/`UseAuthorization()` middleware |
| **`.http` file auth flow** | AI originally generated `Invoices.http` without any auth headers, then tried using `{{getToken.response.body.accessToken}}` as a file-level variable (not supported by REST Client) | Fixed to use `@authToken = YOUR_TOKEN_HERE` file-level variable with manual copy-paste flow, which is the only reliable approach for REST Client |
| **Rate limiter API mismatch** | AI used `options.AddFixedWindowLimiter()` which doesn't exist in .NET 10 — the API changed to `options.AddPolicy<string>()` with `RateLimitPartition.GetFixedWindowLimiter()` | Changed to correct .NET 10 API with `AddPolicy<string>` |

## 5. What Parts I Wrote, Corrected, or Significantly Changed Myself

### Wholly my architectural decisions (not AI-generated):

- **Modular Monolith + Clean Architecture + CQRS** pattern choice
- **Schema-per-tenant** isolation strategy (vs. database-per-tenant or discriminator column)
- **InvoiceStatus lifecycle** design (5 states, terminal states, allowed transitions)
- **Invoice number format** (`INV-{year}-{sequence}`) and generation approach
- **Dashboard design** (what fields, how aggregated)
- **Indexing strategy** (which columns, composite indexes)
- **Technology choices**: MediatR over raw DI, Mapster over AutoMapper, MassTransit Outbox, Finbuckle
- **Testing strategy**: What to unit-test vs. integration-test vs. architecture-test
- **Event storming workshop**: The entire `docs/process/event-storming-session.md` was AI-facilitated but I directed every domain event, aggregate boundary, and hot spot

### Significantly corrected/rewritten:

- **Invoice.Create factory method**: AI generated a basic constructor; I added the full validation with `Result<T>` pattern, domain event publishing, and `RecalculateTotals()`
- **Status transition methods**: AI generated simple property setters; I added domain event publishing, validation of allowed transitions, and terminal state checks
- **Repository ListAsync**: AI generated a basic query; I added search term filtering across multiple fields and date range filtering
- **Dashboard aggregation**: AI generated a raw SQL approach; I moved to in-memory aggregation with proper edge case handling (missing statuses default to 0)
- **Program.cs**: Significant restructuring of DI pipeline, middleware ordering, and module composition
- **AppHost**: Rewrote to use proper Aspire integration pattern with `WaitFor` dependencies

### Written entirely by me:

- The ADR (`docs/architecture/architecture-decision-record.md`) — AI helped structure it but all 14 sections of technical decisions are mine
- The event storming domain context and all persona definitions
- The "Known Limitations" and "What I Would Improve" sections of SOLUTION_NOTES.md
- The Azure deployment strategy and monitoring plan

### Post-implementation review fixes (corrected by me with AI assistance):

After a thorough expectation gap analysis (`docs/review/expectation-gap-analysis.md`), three high-severity gaps were identified and fixed:

- **Fix 1 — FluentValidation → 400**: Created `ValidationExceptionHandler.cs` to catch `FluentValidation.ValidationException` and return 400 with validation errors, instead of the previous 500 Internal Server Error. Registered it before `GlobalExceptionHandler` in the DI pipeline (order matters for ASP.NET Core exception handlers).
- **Fix 2 — JWT Bearer authentication**: Created `AuthenticationExtensions.cs` with symmetric-key JWT validation, added `[Authorize]` on `InvoicesController`, created `AuthController` with `POST /api/auth/token` for dev token generation, and added `UseAuthentication()`/`UseAuthorization()` middleware. All integration tests updated to pass JWT tokens.
- **Fix 3 — Rate limiting**: Added `AddRateLimiter()` with a fixed-window policy (100 requests/minute, no queuing) and `[EnableRateLimiting("GlobalLimit")]` on the controller. Returns 429 with `Retry-After` header when exceeded.
- **Bonus fix — Database startup**: Corrected `DatabaseExtensions.cs` to always use `MigrateAsync()` — the previous `EnsureCreatedAsync()` fallback created tables from a stale model snapshot missing the `TenantId` column.
- **Bonus fix — Documentation**: Updated `README.md` with auth section, security features, token endpoint, `.http` file instructions, and corrected the solution file reference from `.sln` to `.slnx`. Updated `AI_USAGE.md` (this file) to reflect all post-review changes.

---

## Human Audit Notes (from Event Storming Session)

The event storming session (`docs/process/event-storming-session.md`) was run as an AI-facilitated workshop. Key observations:

- **Domain Expert persona**: The AI correctly identified the core events (`InvoiceCreated`, `InvoiceStatusChanged`, `InvoiceOverdueDetected`) but initially missed unhappy-path events like `InvoiceStatusTransitionRejected` and `InvoiceNumberDuplicateDetected`. I added these.
- **Engineer persona**: The AI proposed reasonable aggregate boundaries (Invoice as AR, LineItem as child entity). I agreed with this — splitting LineItem into its own aggregate would violate transactional consistency for computed totals.
- **QA persona**: The AI surfaced important hot spots (concurrent status updates, duplicate invoice numbers, overdue detection race conditions). I added the multi-tenant-specific hot spots (schema routing misconfiguration, cross-schema query performance).
- **Architect persona**: The AI correctly identified MassTransit/Azure Service Bus topology (fan-out exchange, per-consumer queues). I added the Outbox pattern details and the schema-per-tenant migration coordination concern.
