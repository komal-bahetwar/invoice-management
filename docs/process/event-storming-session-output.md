# Event Storming Session Output — Multi-Tenant Invoice Management Module

> This is the consolidated output from running the event storming workshop defined in [`event-storming-session.md`](./event-storming-session.md). It captures the results of all three phases (Big Picture, Process Modeling, Software Design) and the four final deliverables.

---

## PHASE 1 — Big Picture (Domain Events Timeline)

**Domain Expert brain-dump (unordered):**

| Event | Description |
|---|---|
| `InvoiceCreated` | New invoice created in Draft status |
| `InvoiceSent` | Draft invoice sent to customer |
| `InvoicePaid` | Payment received; invoice now terminal |
| `InvoiceMarkedAsOverdue` | System detected DueDate passed on a Sent invoice |
| `InvoiceCancelled` | Invoice cancelled (Draft or Sent only) |
| `InvoiceStatusTransitionRejected` | Invalid status transition attempted |
| `InvoiceNumberDuplicateDetected` | Invoice number collision within same tenant/year |
| `InvoiceNotFound` | Lookup by ID returned nothing |
| `InvoiceLineItemAdded` | Line item added to draft invoice |
| `DashboardRefreshed` | Dashboard projection recalculated |
| `InvoiceDueDateApproaching` | 7-day warning before due date (future scope) |

**Facilitator — consolidated timeline:**

1. `InvoiceCreated` → Invoice enters `Draft`
2. `InvoiceSent` → `Draft` → `Sent`; begins countdown to due date
3. `InvoiceDueDateApproaching` — policy trigger at DueDate - 7 days
4. `InvoiceMarkedAsOverdue` — policy trigger at DueDate < Now (auto-transition)
5. `InvoicePaid` — manual/simulated; `Sent` or `Overdue` → `Paid` (terminal)
6. `InvoiceCancelled` — manual; `Draft` or `Sent` → `Cancelled` (terminal)
7. `InvoiceStatusTransitionRejected` — fired when a disallowed transition is attempted
8. `DashboardRefreshed` — triggered reactively by InvoiceCreated or InvoiceStatusChanged

---

## PHASE 2 — Process Modeling

**Command → Event mappings:**

| Command | Actor | Aggregate | Event |
|---|---|---|---|
| `CreateInvoice` | TenantUser | Invoice | `InvoiceCreated` |
| `SendInvoice` | TenantUser | Invoice | `InvoiceSent` → `InvoiceStatusChanged` |
| `PayInvoice` | TenantUser | Invoice | `InvoicePaid` → `InvoiceStatusChanged` |
| `CancelInvoice` | TenantUser | Invoice | `InvoiceCancelled` → `InvoiceStatusChanged` |
| `DetectOverdueInvoices` | SystemScheduler | Invoice (set) | `InvoiceMarkedAsOverdue` → `InvoiceStatusChanged` + `InvoiceOverdueDetected` |
| `GetInvoice` | TenantUser | — (read) | — |
| `ListInvoices` | TenantUser | — (read) | — |
| `GetDashboard` | TenantUser | — (read) | — |

**Policies:**

| When | Then |
|---|---|
| `InvoiceCreated` OR `InvoiceStatusChanged` | → `RefreshDashboardProjection` |
| DueDate elapsed (clock tick) | → `DetectOverdueInvoices` for all Sent invoices past DueDate |

**External systems:**

| System | Direction | Consumes/Publishes |
|---|---|---|
| Identity Provider (Duende IdentityServer) | Upstream | Publishes JWT with tenant claims; API validates |
| Notification Service | Downstream | Consumes `InvoiceStatusChanged`, sends email |
| Analytics Service | Downstream | Consumes all invoice events for reporting |
| Audit Service | Downstream | Consumes all domain events for compliance |

**MassTransit topology:**

```
Exchange: invoice-events (fan-out)
├── Queue: notification-service-invoice-status → Notification Service
├── Queue: analytics-service-invoice-events → Analytics Service
└── Queue: audit-service-all-events → Audit Service

Event payload (all events):
  EventId: Guid
  TenantId: string
  InvoiceId: Guid
  Timestamp: DateTimeOffset
  EventType: string
  + type-specific fields (Status, Amount, Currency, etc.)
```

**Read models:**

| Model | Consumer | Data |
|---|---|---|
| `InvoiceDetailView` | TenantUser | Full invoice + line items |
| `InvoiceListView` | TenantUser | Paginated invoice summaries with filters |
| `DashboardView` | TenantUser | Aggregated counts/amounts by status |

---

## PHASE 3 — Software Design

**Aggregates:**

| Aggregate | Type | Invariants |
|---|---|---|
| **Invoice** | Aggregate Root | ≥1 line item; DueDate ≥ IssueDate; status transitions follow allowed edges; Paid/Cancelled terminal; invoice number unique per tenant; computed totals (SubTotal, TaxAmount, TotalAmount) immutable from outside |
| **InvoiceLineItem** | Child Entity (owned) | Quantity > 0; UnitPrice > 0; TotalPrice = Quantity × UnitPrice |
| **DashboardProjection** | Read Model (separate) | Eventually consistent; refreshed on InvoiceCreated and InvoiceStatusChanged |

**Bounded contexts:**

```
┌──────────────┐     ┌──────────────┐     ┌────────────────┐
│   Identity   │────▶│  Invoicing   │────▶│ Notifications  │
│  (upstream)  │     │  (this module)│    │  (downstream)  │
└──────────────┘     └──────┬───────┘     └────────────────┘
                            │
                   ┌────────┴────────┐
                   ▼                 ▼
            ┌───────────┐    ┌───────────┐
            │ Analytics │    │   Audit   │
            │(downstream)│   │(downstream)│
            └───────────┘    └───────────┘
```

---

### Policy: Overdue Detection (clock-driven)

```
[SystemScheduler]
    │
    │ (every N minutes)
    ▼
[DetectOverdueInvoices Command]
    │
    │ UPDATE Invoices
    │ SET Status = 'Overdue'
    │ WHERE Status = 'Sent'
    │   AND DueDate < @now
    │
    ▼
«InvoiceMarkedAsOverdue»
    │
    ├──▶ «InvoiceStatusChanged» (OldStatus: Sent, NewStatus: Overdue)
    └──▶ «InvoiceOverdueDetected» (OutstandingAmount, Currency, DueDate)

    --[Policy]--> RefreshDashboardProjection
    --[Policy]--> Publish to MassTransit → Notifications/Analytics/Audit
```

### Hot Spots Identified During Process Modeling

| Hot Spot | Severity | Owner | Resolution |
|---|---|---|---|
| Invoice number collision under concurrency | High | Engineer | Unique constraint + retry with sequence counter |
| Concurrent status updates on same invoice | High | Engineer | RowVersion optimistic concurrency |
| Overdue detection racing with manual PayInvoice | Medium | QA | PayInvoice takes precedence; optimistic concurrency handles the conflict |
| Outbox fails to publish event | Medium | Architect | MassTransit Outbox retry + dead-letter queue |
| Dashboard stale between refreshes | Low | Product Owner | Accept eventual consistency for assessment scope |
| Pagination drift during concurrent inserts | Low | QA | Offset pagination acceptable; keyset for production |
| Schema provisioning race during tenant onboarding | High (future) | Architect | Lock or idempotent schema creation |
| Cross-tenant data leakage via misconfigured schema routing | Critical (future) | Architect | Integration test per-tenant isolation |

---

## FINAL OUTPUT

### 1. CONSOLIDATED MODEL

```
┌─────────────────────────────────────────────────────────────────────┐
│                         INVOICE MANAGEMENT                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [TenantUser]                                                      │
│       │                                                            │
│       ├──(CreateInvoice)──▶ (Invoice Aggregate)                    │
│       │                          ==> «InvoiceCreated»              │
│       │                              │                             │
│       ├──(SendInvoice)────▶ (Invoice Aggregate, if Draft)          │
│       │                          ==> «InvoiceStatusChanged»        │
│       │                              │                             │
│       ├──(PayInvoice)─────▶ (Invoice Aggregate, if Sent/Overdue)   │
│       │                          ==> «InvoiceStatusChanged»        │
│       │                              │                             │
│       ├──(CancelInvoice)──▶ (Invoice Aggregate, if Draft/Sent)     │
│       │                          ==> «InvoiceStatusChanged»        │
│       │                              │                             │
│       ├──(GetInvoice)─────▶ Read: InvoiceDetailView                │
│       ├──(ListInvoices)───▶ Read: InvoiceListView (paginated)      │
│       └──(GetDashboard)───▶ Read: DashboardView                    │
│                                                                     │
│  [SystemScheduler]                                                 │
│       │                                                            │
│       └──policy: DueDate elapsed──▶ (DetectOverdueInvoices)        │
│           Scan all Sent invoices WHERE DueDate < NOW               │
│           For each:                                                │
│               ==> «InvoiceStatusChanged» (Sent → Overdue)          │
│               ==> «InvoiceOverdueDetected»                         │
│                                                                     │
│  Policies:                                                         │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │ whenever InvoiceCreated | InvoiceStatusChanged              │   │
│  │   → RefreshDashboardProjection                              │   │
│  │   → Publish via MassTransit Outbox                          │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  External Systems:                                                 │
│  ┌──────────────────┐                                              │
│  │ Identity Provider │──▶ JWT (tenant claims) ──▶ API validation   │
│  └──────────────────┘                                              │
│                                                                     │
│  API ──MassTransit──▶ Notification Service  (InvoiceStatusChanged) │
│     ├────────────────▶ Analytics Service     (all events)          │
│     └────────────────▶ Audit Service         (all events)          │
│                                                                     │
│  Tenant Isolation: Schema-per-tenant (Finbuckle)                   │
│  ┌──────────────┬──────────────┬──────────────┐                   │
│  │ tenant_001   │ tenant_002   │ tenant_003   │                   │
│  │ .Invoices    │ .Invoices    │ .Invoices    │                   │
│  │ .LineItems   │ .LineItems   │ .LineItems   │                   │
│  └──────────────┴──────────────┴──────────────┘                   │
│         ▲              ▲              ▲                            │
│         └──────────────┴──────────────┘                            │
│                X-Tenant-Id header → Finbuckle                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2. AGGREGATES & INVARIANTS

#### Invoice (Aggregate Root)

| # | Invariant | Enforcement | Type |
|---|---|---|---|
| I1 | At least 1 line item required | Factory validation; `Result<T>` on Create | Business |
| I2 | DueDate ≥ IssueDate | Factory validation | Business |
| I3 | TaxRate ∈ [0, 100] | Factory validation | Business |
| I4 | CustomerName, CustomerEmail required | Factory validation | Business |
| I5 | Currency must be 3-letter ISO 4217 | Factory validation | Business |
| I6 | Status transitions follow allowed edges only | `MarkAsSent()`, `MarkAsPaid()`, etc. | Business |
| I7 | Paid and Cancelled are terminal — no further transitions | Aggregate method guards | Business |
| I8 | SubTotal, TaxAmount, TotalAmount are computed — never settable | Private set; `RecalculateTotals()` | Technical |
| I9 | InvoiceNumber unique per tenant | Unique DB constraint | Technical |
| I10 | Optimistic concurrency via RowVersion | `byte[] RowVersion` + `[Timestamp]` | Technical |

#### InvoiceLineItem (Child Entity)

| # | Invariant | Enforcement |
|---|---|---|
| L1 | Quantity > 0 | Factory validation |
| L2 | UnitPrice > 0 | Factory validation |
| L3 | TotalPrice = Quantity × UnitPrice | Computed in constructor |
| L4 | Description required, max 500 chars | Factory validation |

#### DashboardProjection (Read Model)

| # | Invariant | Enforcement |
|---|---|---|
| D1 | Eventually consistent with invoice data | Refreshed on InvoiceCreated and InvoiceStatusChanged |
| D2 | All 5 statuses always present in byStatus dict (default 0) | Handler ensures defaults |
| D3 | Single tenant scope — never cross-tenant | Finbuckle schema routing |

### 3. HOT SPOTS

#### Resolved (must-test)

| Hot Spot | Invariant/Test | Status |
|---|---|---|
| **Concurrent status updates** | I10: RowVersion concurrency check; `DbUpdateConcurrencyException` tested in integration tests | Resolved |
| **Duplicate invoice numbers** | I9: Unique DB constraint; handler checks pre-insert; integration test verifies collision returns error | Resolved |
| **Invalid status transitions** | I6/I7: Aggregate methods reject with `Result.Failure`; unit tests cover all edges | Resolved |
| **Zero line items** | I1: Factory validation; unit test `Create_WithEmptyLineItems_ShouldFail` | Resolved |
| **DueDate before IssueDate** | I2: Factory validation; unit test `Create_WithDueDateBeforeIssueDate_ShouldFail` | Resolved |
| **Negative money** | `Money` value object rejects negative amounts; unit tested | Resolved |

#### Open (discussion needed)

| Hot Spot | Risk | Recommended Action |
|---|---|---|
| **Overdue detection vs manual PayInvoice race** | Scheduler marks invoice Overdue while user simultaneously pays it | PayInvoice wins; scheduler retries and finds Status ≠ Sent → skips. Optimistic concurrency on RowVersion handles any write conflict |
| **Invoice number collision at scale** | Current: `COUNT + 1` per tenant/year. TOCTOU race under high concurrency | For production: switch to sequence table per tenant or HiLo pattern |
| **Outbox at-least-once delivery** | Duplicate events if retry fires but publish succeeded | Consumers must be idempotent; use EventId as deduplication key |
| **Pagination consistency** | New invoices inserted between page 1 and page 2 cause drift | Acceptable for assessment; keyset pagination for production |
| **Dashboard staleness** | Projection refreshed on events but not real-time | Accept eventual consistency; add cache TTL for production |
| **Overdue detection at scale** (enterprise) | Scanning all tenants' Sent invoices could be expensive with hundreds of tenants | Per-tenant scheduled task or filtered index on `(Status, DueDate)` |
| **Tenant provisioning race** (enterprise) | Schema creation racing with first API request for a new tenant | Idempotent `CREATE SCHEMA IF NOT EXISTS` or pre-provision during tenant onboarding |
| **Cross-tenant data leakage** (enterprise) | Misconfigured `X-Tenant-Id` resolution could route to wrong schema | Integration test: create invoice in tenant A, verify invisible to tenant B |

### 4. ASSIGNMENT MAPPING

| Requirement | Model Element | Notes |
|---|---|---|
| **1. Create an invoice** | `POST /api/invoices` → `CreateInvoiceCommand` → Invoice Aggregate → `InvoiceCreated` | Returns 201 with `InvoiceDto` |
| **2. List invoices** | `GET /api/invoices` → `ListInvoicesQuery` → InvoiceListView | Paginated, filterable by status/search/dates |
| **3. View invoice details** | `GET /api/invoices/{id}` → `GetInvoiceQuery` → InvoiceDetailView | Returns 404 if not found |
| **4. Update invoice status** | `PATCH /api/invoices/{id}/status` → `UpdateInvoiceStatusCommand` → `InvoiceStatusChanged` | Enforces lifecycle; 409 on invalid transition |
| **5. Dashboard summary** | `GET /api/invoices/dashboard` → `GetDashboardQuery` → DashboardView | Aggregated counts/amounts by status |

#### What the Model Surfaced That the Assignment Doesn't Explicitly Require

| Surfaced Concern | Discussion |
|---|---|
| **Overdue detection policy** | Assignment says "update invoice status" but doesn't specify how invoices become overdue. The model treats it as a system-driven clock policy, not a user action. Production would need a background job (Hangfire, Azure Function). |
| **Invoice number format and generation** | Not specified in requirements. Model chose `INV-{year}-{sequence:D6}`, unique per tenant. The TOCTOU race in generation is a known limitation. |
| **Domain events for integration** | Assignment doesn't mention messaging. Model surfaces InvoiceCreated, InvoiceStatusChanged, InvoiceOverdueDetected as integration events because a real SaaS platform would need downstream consumers (notifications, analytics, audit). |
| **Dashboard as eventually consistent projection** | For assessment scope, dashboard is computed in-memory. The model surfaces that production should use a pre-aggregated read model refreshed on domain events. |
| **Optimistic concurrency** | The model explicitly calls out RowVersion for concurrent status updates — a production concern the assignment doesn't mention but good engineering leaders anticipate. |
| **Tenant provisioning** | Schema-per-tenant is specified in the ADR, but the model surfaces the provisioning workflow (schema creation, connection string management) as a hot spot the assignment scope doesn't cover. |
