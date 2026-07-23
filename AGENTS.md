# AGENTS.md

## Project

Multi-tenant invoice management API for a SaaS platform. Built as a Qwiik Technical Assessment.
.NET 10, C# 14, ASP.NET Core 10, SQL Server 2022 (Docker), Modular Monolith + Clean Architecture + CQRS.

**Tech stack:** MediatR, FluentValidation, Mapster, Finbuckle.MultiTenant (schema-per-tenant), MassTransit (Outbox), Duende IdentityServer 8.0+ (scaffolded), Serilog + Seq, Swashbuckle + Scalar, .NET Aspire, Polly.
**Testing:** xUnit, Shouldly, NSubstitute, Bogus, Testcontainers, WebApplicationFactory, Respawn, NetArchTest.

## Commands

```bash
# Build
dotnet build backend/InvoiceManagement.sln

# Run (all services via Aspire — API + SQL Server + Seq)
dotnet run --project backend/src/InvoiceManagement.AppHost

# Run API only (needs SQL Server running separately)
dotnet run --project backend/src/InvoiceManagement.Api

# Run DB migrations (standalone migrator — API never runs migrations)
dotnet run --project backend/src/InvoiceManagement.Migrator

# Test all
dotnet test backend/InvoiceManagement.sln

# Test single project
dotnet test backend/src/modules/invoicing/tests/InvoiceManagement.Modules.Invoicing.UnitTests

# Architecture tests
dotnet test backend/tests/InvoiceManagement.ArchitectureTests
```

## Project Structure

```
backend/
├── src/
│   ├── common/
│   │   ├── InvoiceManagement.Common.Domain/          # Shared kernel: BaseEntity, DomainEvent, Money
│   │   └── InvoiceManagement.Common.Infrastructure/  # Cross-cutting: MassTransit, Finbuckle, Serilog
│   ├── modules/
│   │   └── invoicing/
│   │       ├── Domain/       Entities, enums, domain events, repository interfaces
│   │       ├── Application/  Commands, queries, handlers, DTOs, validators, contracts, behaviors
│   │       ├── Infrastructure/ EF Core DbContext, repository impl, entity configs, migrations
│   │       ├── Api/          Controllers
│   │       └── tests/        UnitTests/ + IntegrationTests/
│   ├── InvoiceManagement.Api/         Thin API host — composes modules, middleware
│   ├── InvoiceManagement.AppHost/     .NET Aspire orchestration
│   └── InvoiceManagement.Migrator/    Standalone DB migration console app
└── tests/
    ├── InvoiceManagement.Api.IntegrationTests/   WebApplicationFactory-based API tests
    ├── InvoiceManagement.ArchitectureTests/      NetArchTest layer dependency enforcement
    └── InvoiceManagement.Common.Tests/           Shared kernel value object tests
```

## Code Style

- **Naming:** `{Action}{Entity}Command` / `{Action}{Entity}Query`, handlers match request name + `Handler`, validators match request name + `Validator`
- **File organization:** Each command/query in its own folder (e.g., `Commands/CreateInvoice/` containing Command, Handler, Validator). DTOs in `DTOs/`. EF configs in `Data/Configurations/`.
- **Dependency direction:** Domain → no deps. Application → Domain only. Infrastructure → Domain + Application. Api → Application + Infrastructure.
- **EF Core:** Use `IEntityTypeConfiguration<T>` classes, not data annotations
- **Mapping:** Use Mapster, never AutoMapper
- **DI:** Use Scrutor for assembly scanning and auto-registration
- **Resilience:** Use Polly for all external calls

```csharp
// Command handler pattern
public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<Result<InvoiceDto>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = Invoice.Create(/* ... */);
        await _repository.AddAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return invoice.Adapt<InvoiceDto>();
    }
}
```

## Testing

- **Framework:** xUnit with Shouldly for assertions, NSubstitute for mocking, Bogus for test data
- **Unit tests:** `backend/src/modules/{module}/tests/{Module}.UnitTests/` — test domain entities, value objects, validators, command/query handlers with mocked dependencies
- **Integration tests:** `backend/src/modules/{module}/tests/{Module}.IntegrationTests/` — real SQL Server via Testcontainers, reset with Respawn between tests
- **API tests:** `backend/tests/InvoiceManagement.Api.IntegrationTests/` — use `WebApplicationFactory`, verify HTTP contracts, error responses, pagination, tenant isolation
- **Architecture tests:** `backend/tests/InvoiceManagement.ArchitectureTests/` — enforce: Domain has no EF/ASP.NET deps; Application has no Infrastructure deps; modules don't reference each other
- **Common tests:** `backend/tests/InvoiceManagement.Common.Tests/` — value objects like Money

## Git Workflow

- Branch from `main`, prefix with `feat/`, `fix/`, or `chore/`
- Commit messages: conventional commits (`feat:`, `fix:`, `chore:`, `docs:`, `test:`)
- Squash merge to main

## Do Not Modify

- `*.csproj` — package references must be discussed before changing
- Migration files under `Migrations/` — generate with EF Core tooling, never edit by hand
- `appsettings.Development.json` — contains local secrets; use user secrets or .NET Aspire for config
- `docs/architecture/architecture-decision-record.md` — update only when an architectural decision changes
- `docs/requirement.md` — assessment specification, immutable
- `docs/tech-stack/` — reference documents, treat as read-only
