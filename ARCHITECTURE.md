# ARCHITECTURE.md — Pamyat Ryadom

Architecture and monorepo structure for Pamyat Ryadom.

## 1. Overview

Pamyat Ryadom is a managed cemetery-care service. A client orders upkeep for a known burial
site; the order is dispatched to an executor who visits the cemetery and files a photo/video
report; the client reviews the result and pays. The system has three main parts:

1. **Backend** (ASP.NET Core Web API, .NET 10) — business logic and the API.
2. **Frontend** (Next.js + React + TypeScript) — client-facing web app, dispatcher/ops console.
3. **PostgreSQL** — system of record. **S3-compatible object storage** — photo/video evidence.

Everything runs through **Docker Compose**.

```
Client / Executor (browser)
        |  HTTP
        v
   Next.js (App Router, SSR)
        |  REST / JSON
        v
   ASP.NET Core Web API
        |            \
        | EF Core     \ presigned URLs
        v              v
   PostgreSQL      S3-compatible object storage
                        ^
                        |  YooKassa (redirect-confirmation payments)
```

## 2. Monorepo structure

```
dan/
├── backend/
│   ├── PamyatRyadom.sln
│   ├── .config/dotnet-tools.json      # dotnet-ef as a local tool
│   ├── src/PamyatRyadom.Api/          # ASP.NET Core Web API (.NET 10)
│   │   ├── Controllers/               # thin: validate input, call a Service, return a DTO
│   │   ├── Services/<Module>/         # business logic, one folder per module (see §3)
│   │   ├── Dtos/<Module>/             # public API contract — never expose entities
│   │   ├── Data/                      # AppDbContext, IEntityTypeConfiguration<T>, Migrations/
│   │   └── Program.cs
│   └── tests/PamyatRyadom.Api.Tests/  # xUnit + Testcontainers PostgreSQL
├── frontend/                          # Next.js 14 App Router + React 18 + TypeScript strict
│   ├── app/                           # routes
│   ├── components/                    # ui/ primitives, site/ chrome, home/ sections, auth/
│   ├── lib/                           # api client, auth helpers, utilities
│   ├── middleware.ts                  # cheap cookie-presence gate on /app, /admin
│   ├── public/                        # static assets — must exist, the Dockerfile copies it
│   ├── tailwind.config.js             # design-system tokens (direction A «Тихий сад»)
│   └── Dockerfile                     # multi-stage → standalone Next.js runtime
├── deploy/                            # migrate.sh, backup-db.sh, nginx config example
├── docs/                              # project documentation
│   └── adr/                           # architecture decision records
├── docker-compose.yml                 # postgres + api + frontend
├── docker-compose.override.yml        # dev-only: adminer, ASPNETCORE_ENVIRONMENT=Development
├── docker-compose.prod.yml            # explicit production override
├── .env.example
├── three wariants design/             # design canvas source of truth (read-only for code tasks)
└── *.md                               # root project instructions (this file, CLAUDE.md, CONVENTIONS.md)
```

## 3. Layering and responsibilities

### Backend (`backend/`)

- Implements business logic and the REST API.
- Business logic lives **outside** controllers — controllers stay thin.
- The public API uses **DTOs**; domain entities are never returned directly.
- DB schema changes go **only** through EF Core migrations.
- Does not render HTML or own any SEO/marketing concerns — that is the frontend's job.

```
Controllers  →  Services/<Module> (business logic)  →  Data (AppDbContext, EF Core)  →  PostgreSQL
     ↑                    ↑
   DTOs              Domain models
```

| Layer | Responsibility |
|---|---|
| Controllers | Routing, input validation, mapping DTO ↔ service call |
| Services/`<Module>` | Business rules, orchestration, calls to Payments/Storage/etc. |
| Data (`AppDbContext`) | Data access through EF Core, `IEntityTypeConfiguration<T>` per module |
| Domain models | Domain entities — not exposed outside the backend |
| Dtos | The public API contract |

Every response uses one envelope, `Dtos/Common/ApiResponse<T>`:

```jsonc
{ "data": { … }, "meta": null, "errors": null }            // success
{ "data": null, "meta": null, "errors": [ { "code": "…", "message": "…", "details": … } ] }
```

`Program.cs` normalizes the two responses the framework would otherwise emit in its own shape —
rate-limiter rejections (429) and `[ApiController]` model-binding failures (400) — into the same
envelope, so a client never has to branch on error format.

### Frontend (`frontend/`)

- Client-facing web app: browse a burial site, place an order, track visit status, view photo
  evidence, pay, open a dispute.
- Also serves the internal dispatcher/ops surfaces (assign visits, review evidence, resolve
  disputes) behind the `dispatcher`/`qa`/`support`/`finance`/`admin` roles.
- Talks to the backend over REST only; never touches Postgres or the S3 bucket directly — media
  is fetched/uploaded via presigned URLs the backend issues.
- Tailwind CSS, mobile-first (most clients are expected to use the service from a phone). The
  token set in `tailwind.config.js` + `app/globals.css` is design direction A («Тихий сад») from
  `three wariants design/` — colours, type scale, radii and shadows are named tokens
  (`bg-paper`, `text-ink-2`, `rounded-card`, …), so components never hardcode a hex value.
  Spectral (display) and Manrope (UI) are self-hosted through `next/font`.
- Two addresses reach the API and both are needed: the browser uses `NEXT_PUBLIC_API_URL` (public
  host:port, baked into the client bundle at build time), while Server Components run inside the
  frontend container and use `API_INTERNAL_URL` (`http://api:8080/api/v1`) — see
  `lib/api/client.ts#getBaseUrl`.
- Session handling: `middleware.ts` does a presence-only cookie check on `/app` and `/admin` to
  save an obviously-anonymous visitor a round trip; the real check is `lib/auth/server.ts`
  `getServerUser()`, which calls `GET /auth/me` on every render and fails closed to "logged out".

## 4. Module boundaries

**Identity**, **BurialSites**, **Catalog**, **Orders/Estimates**, **Media**, **Payments** and
**Dispatch/Visits** exist in code today; Disputes and Subscriptions describe intended boundaries
for the tasks that build them. An order now runs the whole way — placed, priced, paid, dispatched,
photographed, reviewed and accepted — with one qualification: the payment provider is a stub, so
no money actually moves.

- **Identity** *(implemented)* — accounts, roles, email/SMS OTP login, opaque session tokens (hash
  stored server-side), MFA for privileged roles, consent logging. Owns who is allowed to do what.
  Lives in `Services/Auth/` + `Controllers/AuthController.cs`, exposed under `/api/v1/auth`. Its
  9 tables (`users`, `auth_identities`, `auth_sessions`, `otp_codes`, `mfa_secrets`,
  `consent_logs`, `legal_documents`, `legal_acceptances`, `security_audit_logs`) ship in the
  `InitialIdentity` migration. Access control is the `[RequireRole]` action filter, not ASP.NET
  Core's authentication middleware — the session is an opaque cookie resolved against the DB on
  every request, so there is no `ClaimsPrincipal` to populate.
- **BurialSites** *(implemented)* — the burial site as a real-world object: cemetery, plot
  location, the deceased's details, photos/notes tied to the site itself (not to a specific
  visit). Access is a per-user permission (`view` / `manage`) resolved by
  `IBurialSiteService.ResolvePermissionAsync`, so a family can share one record.
- **Catalog** *(implemented)* — the sellable service catalog: care packages, subscription plans
  and their checklists. Versioned: a published version is immutable and a change means a new
  version, because an order has to keep the terms it was sold under. Admin-only to edit
  (`/api/v1/admin/catalog`), public to read.
- **Orders/Estimates** *(implemented)* — an order placed by a client against a burial site and a
  catalog selection, its versioned estimate, and its 16-status lifecycle.
  `Models/Orders/OrderStateMachine.cs` holds the transition table and is the single authority;
  `OrderService.Move()` is the only writer of `order.Status`. Acceptance is version-specific:
  publishing a corrected estimate supersedes the previous one and invalidates an acceptance that
  was not yet paid (BR-002). `OrderStatuses.QueueGroup()` classifies a status for the staff
  queue, and every status must belong to exactly one group.
- **Dispatch/Visits** *(implemented)* — assigns a paid order to an executor as a visit, carries
  the checklist copied onto that visit at assignment, and holds the photo report through QA. The
  only module that knows payout terms, and it keeps them out of client-facing shapes structurally:
  `VisitDto` has `PayoutRub`, `VisitReportDto` does not have the field at all. QA stands between a
  filed report and the client — `GetReportForClientAsync` returns only an approved visit (BR-010) —
  and a report is refused without every checklist line answered plus a "before" and an "after"
  photograph (BR-008).
- **Media** *(implemented for burial sites and orders)* — presigned upload/download URLs against
  the S3-compatible bucket, and the record of which photo/video belongs to which owning record.
  The bytes never pass through the API process: the browser PUTs straight to storage, and only
  the completion call verifies the file really is an image, strips EXIF by re-encoding to WebP
  and builds a thumbnail. Access is decided per owner type in `CanReadOwnerAsync` /
  `CanWriteOwnerAsync`, which deny by default — a new owner type cannot inherit open access.
  Order photos are readable by the staff who need them to work (dispatcher, qa, support, admin,
  superadmin) and writable only by the client who owns the order, while it is still editable.
  Photos are permanent private records, not short-retention outputs — nothing here auto-expires.
- **Payments** *(infrastructure implemented, provider stubbed)* — `IPaymentProvider` is the whole
  contract: create, read, refund. There is no "mark as paid", by design — a payment becomes paid
  because the provider was asked directly, so a callback can only trigger the asking and can never
  assert an outcome. Idempotency is a unique `payments.order_ref` index plus a stored
  `Idempotence-Key`, which makes a retry the same charge rather than a second one. Refunds are part
  of the interface rather than a later addition; a full refund moves the order to `refunded`, a
  partial one leaves it alone because the work still happened. The provider is selected by
  `IHostEnvironment`, never by configuration: `StubPaymentProvider` throws if constructed in
  Production, and Production registers a provider that throws on resolve until the YooKassa adapter
  is written — a deployment with no way to take money fails loudly instead of looking healthy.
- **Reports/Disputes** — the report itself lives in Dispatch (above). What remains here is the
  dispute flow when a client is unsatisfied: an order can be moved to `disputed` today, but there
  is no case record, no resolution path and no refund trigger behind it yet.
- **Subscriptions** — recurring care plans that generate orders on a schedule, and their
  billing/renewal/cancellation lifecycle.
- **Audit** — an append-only log of security- and business-relevant events (logins, status
  changes, payment state transitions, dispute resolutions) for support and compliance review.
  Never stores PII values, only identifiers and event types.

## 5. Data (`Data/` module conventions)

- `PamyatRyadom.Api.Data.AppDbContext` — a single EF Core DbContext.
- `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`) — table/column names are
  snake_case in Postgres while C# stays PascalCase.
- `IEntityTypeConfiguration<T>` organized per module under `Data/Configurations/`.
- `AppDbContext.SaveChanges*` stamps `created_at`/`updated_at` by reflecting over entity
  properties — see `CONVENTIONS.md` for the exact rule.
- Migrations live in `Data/Migrations/`; `dotnet-ef` is a local tool pinned in
  `backend/.config/dotnet-tools.json`.
- Full naming/type conventions (ids, money, status, audit columns): `CONVENTIONS.md`.

## 6. Running the stack

Primary way to run everything is Docker Compose. The base `docker-compose.yml` brings up:

- `postgres` — PostgreSQL 17 (alpine), healthcheck via `pg_isready`.
- `api` — the ASP.NET Core Web API, healthcheck via `GET /api/v1/health/ready` (readiness:
  Postgres reachable, no pending migrations — see below; `GET /api/v1/health` is liveness only
  and checks neither).
- `frontend` — the Next.js standalone build.
- `docker-compose.override.yml` (auto-loaded in dev) adds `adminer` and sets
  `ASPNETCORE_ENVIRONMENT=Development`.
- `docker-compose.prod.yml` is an explicit production override (`-f docker-compose.yml -f
  docker-compose.prod.yml`) — it does **not** auto-load, so dev conveniences never leak into
  production.

All configuration comes from `.env` (see `.env.example`), including the placeholders for SMTP,
YooKassa, and S3 credentials that later module tasks wire up.

**Migrations are not applied on api startup** — deliberately, so a rolling deploy cannot have two
api instances racing to migrate. Run `bash deploy/migrate.sh` (one-off SDK container on the compose
network) or `dotnet ef database update` with an explicit connection string after bringing the stack
up. A fresh volume with no migration applied fails the `api` container's readiness healthcheck
(`GET /api/v1/health/ready` returns 503, `reason: pending_migrations`) instead of looking healthy
while every data endpoint 500s — see `docs/ops/runbook.md`.

## 7. Frozen without a dedicated task

The following do not change as a side effect of a feature task — they need a task of their own:

- **Docker Compose service names** (`postgres`, `api`, `frontend`, `adminer`) and the
  dev/prod override split.
- **The monorepo layout** described in §2 (`backend/`, `frontend/`, `docs/`, root compose files).
- **Migration-only schema changes** — no hand-written DDL against a running database, no
  bypassing `dotnet ef migrations add`.
- The module boundaries in §4 — moving a responsibility from one module to another is an
  architecture decision (record it as an ADR under `docs/adr/`), not an incidental refactor.
