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
| **Documentation** | Generated `README.md`, `SOLUTION_NOTES.md`, this `AI_USAGE.md`, and the `event-storming-session.md` |
| **ADR content** | AI helped structure the Architecture Decision Record but all technical decisions were mine |

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

---

## Human Audit Notes (from Event Storming Session)

The event storming session (`docs/process/event-storming-session.md`) was run as an AI-facilitated workshop. Key observations:

- **Domain Expert persona**: The AI correctly identified the core events (`InvoiceCreated`, `InvoiceStatusChanged`, `InvoiceOverdueDetected`) but initially missed unhappy-path events like `InvoiceStatusTransitionRejected` and `InvoiceNumberDuplicateDetected`. I added these.
- **Engineer persona**: The AI proposed reasonable aggregate boundaries (Invoice as AR, LineItem as child entity). I agreed with this — splitting LineItem into its own aggregate would violate transactional consistency for computed totals.
- **QA persona**: The AI surfaced important hot spots (concurrent status updates, duplicate invoice numbers, overdue detection race conditions). I added the multi-tenant-specific hot spots (schema routing misconfiguration, cross-schema query performance).
- **Architect persona**: The AI correctly identified MassTransit/Azure Service Bus topology (fan-out exchange, per-consumer queues). I added the Outbox pattern details and the schema-per-tenant migration coordination concern.
