# Invoice Management API

Multi-tenant invoice management backend API — a hands-on engineering leadership assessment.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server and Seq)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview)

### Run with .NET Aspire (recommended)

```bash
# Install Aspire workload (first time only)
dotnet workload install aspire

# Start all services: API + SQL Server + Seq
dotnet run --project backend/src/InvoiceManagement.AppHost
```

The API will be available at `https://localhost:7101` and the Scalar API docs at `https://localhost:7101/scalar/v1`.

### Run API only (requires SQL Server running separately)

```bash
# Start SQL Server in Docker
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Pass" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# Run migrations
dotnet run --project backend/src/InvoiceManagement.Migrator

# Start API
dotnet run --project backend/src/InvoiceManagement.Api
```

### Run Tests

```bash
# All tests
dotnet test backend/InvoiceManagement.sln

# Unit tests only
dotnet test backend/src/modules/invoicing/tests/InvoiceManagement.Modules.Invoicing.UnitTests

# Architecture tests
dotnet test backend/tests/InvoiceManagement.ArchitectureTests
```

## Architecture

**Modular Monolith + Clean Architecture + CQRS**

```
API Host (thin) → Invoicing Module → Domain / Application / Infrastructure / API
```

All architectural decisions are documented in `docs/architecture/architecture-decision-record.md`.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/invoices` | Create a new invoice |
| `GET` | `/api/invoices` | List invoices (paginated, filterable) |
| `GET` | `/api/invoices/{id}` | View invoice details |
| `PATCH` | `/api/invoices/{id}/status` | Update invoice status |
| `GET` | `/api/invoices/dashboard` | Invoice summary/dashboard |

## Project Structure

```
backend/
├── InvoiceManagement.sln
├── src/
│   ├── common/
│   │   ├── InvoiceManagement.Common.Domain/          # Shared kernel
│   │   └── InvoiceManagement.Common.Infrastructure/  # Cross-cutting concerns
│   ├── modules/invoicing/
│   │   ├── Domain/         # Entities, enums, domain events, interfaces
│   │   ├── Application/    # Commands, queries, handlers, DTOs, validators
│   │   ├── Infrastructure/ # EF Core, repositories, entity configs
│   │   ├── Api/            # Controllers, module registration
│   │   └── tests/          # Unit + Integration tests
│   ├── InvoiceManagement.Api/       # API host (composition root)
│   ├── InvoiceManagement.AppHost/   # .NET Aspire orchestration
│   └── InvoiceManagement.Migrator/  # Standalone DB migrator
└── tests/
    ├── InvoiceManagement.Api.IntegrationTests/
    ├── InvoiceManagement.ArchitectureTests/
    └── InvoiceManagement.Common.Tests/
```

## Documentation

- [Requirement](./docs/requirement.md) — Assessment specification
- [Architecture Decision Record](./docs/architecture/architecture-decision-record.md) — All technical decisions
- [Event Storming Session](./docs/process/event-storming-session.md) — Domain modeling workshop
- [Solution Notes](./SOLUTION_NOTES.md) — Detailed solution explanation
- [AI Usage](./AI_USAGE.md) — AI tools and how they were used
