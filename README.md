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

The API port is assigned dynamically by Aspire — check the console output or Aspire dashboard for the exact URL. The Scalar API docs are available at `{baseUrl}/scalar/v1`.

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
dotnet test backend/InvoiceManagement.slnx

# Unit tests only
dotnet test backend/src/modules/invoicing/tests/InvoiceManagement.Modules.Invoicing.UnitTests

# Architecture tests
dotnet test backend/tests/InvoiceManagement.ArchitectureTests

# API integration tests (requires SQL Server running)
dotnet test backend/tests/InvoiceManagement.Api.IntegrationTests
```

## Authentication

All API endpoints (except `POST /api/auth/token`) require JWT Bearer authentication.

### Generating a Token (Development)

Use the dev-only token endpoint or the `.http` test file:

```bash
# Generate a token
curl -k -X POST https://localhost:<port>/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"dev-user","tenantId":"dev-tenant"}'
```

Pass the `accessToken` from the response as `Authorization: Bearer <token>` on all subsequent requests.

### Using the HTTP Test File

Open `backend/src/InvoiceManagement.Api/Invoices.http` in VS Code (requires [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension):

1. Run the "GENERATE AUTH TOKEN" request
2. Copy the `accessToken` from the response
3. Replace `YOUR_TOKEN_HERE` at the top of the file
4. Run any other request — all use `{{authToken}}`

### Security Features

- **JWT Bearer authentication** — all endpoints protected with `[Authorize]`
- **Schema-per-tenant isolation** — `X-Tenant-Id` header routes data to tenant-specific schemas
- **Rate limiting** — 100 requests/minute per endpoint (returns 429 when exceeded)
- **Optimistic concurrency** — `RowVersion` prevents lost updates on status changes
- **Input validation** — FluentValidation + domain-level validation on all inputs

## Architecture

**Modular Monolith + Clean Architecture + CQRS**

```
API Host (thin) → Invoicing Module → Domain / Application / Infrastructure / API
```

All architectural decisions are documented in `docs/architecture/architecture-decision-record.md`.

## API Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/auth/token` | No | Generate dev JWT (development only) |
| `POST` | `/api/invoices` | Bearer JWT | Create a new invoice |
| `GET` | `/api/invoices` | Bearer JWT | List invoices (paginated, filterable) |
| `GET` | `/api/invoices/{id}` | Bearer JWT | View invoice details |
| `PATCH` | `/api/invoices/{id}/status` | Bearer JWT | Update invoice status |
| `GET` | `/api/invoices/dashboard` | Bearer JWT | Invoice summary/dashboard |

**Required headers on every authenticated request:**
- `Authorization: Bearer <token>`
- `X-Tenant-Id: <tenant-identifier>`

## Project Structure

```
backend/
├── InvoiceManagement.slnx
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
