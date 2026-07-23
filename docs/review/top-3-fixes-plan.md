# Implementation Plan: Top 3 Fixes Before Submission

**Date:** 2026-07-23
**Reference:** `docs/expectation-gap-analysis.md`
**Status:** ✅ All fixes implemented

---

## Fix 1: FluentValidation Exceptions Return 500 Instead of 400

**Severity:** High | **Status:** ✅ Done

### Problem
`ValidationBehavior.cs` threw `FluentValidation.ValidationException` when validators failed. The `GlobalExceptionHandler` caught all exceptions and returned 500.

### Solution
Created `ValidationExceptionHandler.cs` — catches `FluentValidation.ValidationException` and returns 400 with validation errors in ProblemDetails `extensions.errors` array. Registered before `GlobalExceptionHandler` in DI pipeline.

### Files Changed
| File | Action |
|---|---|
| `backend/src/InvoiceManagement.Api/Middleware/ValidationExceptionHandler.cs` | Created |
| `backend/src/InvoiceManagement.Api/Program.cs` | Register handler |
| `backend/tests/.../InvoicesApiTests.cs` | Add `CreateInvoice_ExceedsMaxLength_ShouldReturn400` test |

---

## Fix 2: Add Authentication with JWT Bearer

**Severity:** High | **Status:** ✅ Done

### Problem
Zero authentication code existed. No `[Authorize]`, no JWT middleware, no IdentityServer.

### Solution
Added JWT Bearer authentication with symmetric dev key, `[Authorize]` on all endpoints, and a dev-only token generation endpoint.

### Files Changed
| File | Action |
|---|---|
| `backend/src/InvoiceManagement.Api/InvoiceManagement.Api.csproj` | Add `Microsoft.AspNetCore.Authentication.JwtBearer` |
| `backend/src/InvoiceManagement.Api/appsettings.Development.json` | Add `Jwt` config section |
| `backend/src/InvoiceManagement.Api/Extensions/AuthenticationExtensions.cs` | Created |
| `backend/src/InvoiceManagement.Api/Controllers/AuthController.cs` | Created |
| `backend/src/InvoiceManagement.Api/Program.cs` | Register auth + middleware |
| `backend/src/modules/invoicing/.../Controllers/InvoicesController.cs` | Add `[Authorize]` |
| `backend/tests/.../InvoicesApiTests.cs` | Add JWT to test requests |

---

## Fix 3: Rate Limiting

**Severity:** Medium | **Status:** ✅ Done

### Problem
No rate limiting — no protection against abuse.

### Solution
Added fixed-window rate limiter: 100 requests/minute, no queuing. Returns 429 with `Retry-After` header.

### Files Changed
| File | Action |
|---|---|
| `backend/src/InvoiceManagement.Api/Program.cs` | Add `AddRateLimiter()` + `UseRateLimiter()` |
| `backend/src/modules/invoicing/.../Controllers/InvoicesController.cs` | Add `[EnableRateLimiting("GlobalLimit")]` |

---

## Bonus Fixes

### Database Startup
Changed `DatabaseExtensions.cs` to always call `MigrateAsync()` instead of falling back to `EnsureCreatedAsync()`, which created tables from a stale model snapshot missing `TenantId`.

### Documentation Updates
- `README.md`: Added auth section, security features, token endpoint docs, `.http` file instructions, corrected `.slnx` reference
- `AI_USAGE.md`: Updated with all post-review changes
- `Invoices.http`: Added token generation flow + auth headers on all requests
