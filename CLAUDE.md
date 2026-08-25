# CLAUDE.md — Память рядом (Pamyat Ryadom)

## Project

Pamyat Ryadom is a managed remote cemetery-care service for the Russian market. Clients who
cannot visit a grave in person order recurring or one-off upkeep — cleaning, flowers, small
repairs — and an executor visits the cemetery and files a photo/video report through the app.
It is a managed service, not an open marketplace: clients never pick or contact executors
directly, dispatch assigns the work.

Geography is national, prioritising large cities — not a single-city pilot. Nothing in the
product may assume one city: the cemetery directory is grouped by region, and user-facing copy
says "в городах России" rather than naming a city.

First MVP: order a visit for a known burial site, get it dispatched to an executor, see photo
proof of completion, pay online, raise a dispute if the result is unsatisfactory.

## Stack

- Backend: C# / ASP.NET Core Web API, target runtime .NET 10
- ORM: Entity Framework Core + Npgsql, `EFCore.NamingConventions` (snake_case in Postgres)
- Database: PostgreSQL 17
- Frontend: Next.js 14 (App Router) + React 18 + TypeScript strict + Tailwind CSS 3.4
- Object storage: private S3-compatible bucket in a Russian region (Yandex Object Storage /
  Selectel / VK Cloud) — presigned upload/download URLs, not local disk
- Payments: YooKassa, one-off redirect-confirmation payments
- Infrastructure: Docker Compose (postgres / api / frontend, + adminer in dev), single VPS + nginx
- Repository: GitHub

## Repo layout (monorepo)

- `backend/` — ASP.NET Core Web API (`PamyatRyadom.Api`) + xUnit test project (`PamyatRyadom.Api.Tests`)
- `frontend/` — Next.js App Router app
- `docs/` — project documentation, including `docs/adr/` (architecture decision records)
- `docker-compose.yml` / `docker-compose.override.yml` (dev, adds adminer) / `docker-compose.prod.yml`
- `.env.example` — the source of truth for required environment variables

## Project docs

- `docs/PRODUCT.md` — product brief and MVP scope
- `ARCHITECTURE.md` — system architecture, module boundaries, layering
- `CONVENTIONS.md` — code, schema and naming conventions
- `docs/adr/` — architecture decision records

## Before you change anything

1. Read only the files needed for the task.
2. Propose a short plan and list the files you will change.
3. Wait for confirmation if the task touches architecture, Docker Compose, the DB schema,
   auth/session handling, payments, or object storage.
4. Never touch `backend/` or `frontend/` from a docs-only task, and never touch root-level docs
   from a backend/frontend task, unless the task explicitly says so — parallel tasks assume
   these stay isolated.

## Architecture (see ARCHITECTURE.md for detail)

- Layering: `Controllers -> Services/<Module> -> Data` (EF Core `AppDbContext`). Controllers are
  thin: input validation, call a service, map to a DTO. Business logic lives in `Services/`, never
  in controllers.
- Public API accepts and returns **DTOs** only — domain entities are never serialized directly.
- Every response, success or failure, uses one envelope: `{ "data": …, "meta": …, "errors": [ … ] }`
  (`Dtos/Common/ApiResponse.cs`). Rate-limit rejections and model-binding failures are normalized
  into it in `Program.cs`, so the frontend has exactly one shape to parse
  (`frontend/lib/api/client.ts` unwraps `data` and throws `ApiError` from `errors[0]`).
- Modules: Identity, BurialSites, Catalog, Orders/Estimates, Dispatch/Visits, Media, Payments,
  Reports/Disputes, Subscriptions, Audit.
- Implemented so far: **Identity** (see below). Every other module is still unstarted.
- DB schema changes go through **EF Core migrations only** — no hand-written DDL, no manual
  `ALTER TABLE` against a running database.
- Frontend consumes the backend over REST; it does not talk to Postgres or S3 directly.

## Identity module (implemented)

- Endpoints under `/api/v1/auth`: `POST otp/request`, `POST otp/verify`, `GET me`, `POST logout`,
  `POST logout-all`, `POST mfa/enroll`, `POST mfa/verify`.
- Session cookie is `pamyat_ryadom_auth` (`AuthOptions.CookieName`), HttpOnly + Secure +
  SameSite=Lax. The frontend mirrors the name in `frontend/lib/auth/constants.ts` — change both
  together.
- `[RequireRole]` (`Services/Auth/RequireRoleAttribute.cs`) is how an endpoint requires a session;
  the action then reads `CurrentUser` from `AuthorizedControllerBase`. An action without the
  attribute throws rather than silently running unauthenticated.
- In `Development` the login code is written to the container log by `ConsoleEmailSender`
  (`docker compose logs api`); every other environment uses `SmtpEmailSender`. The choice is made
  by DI at startup from `IHostEnvironment`, so no configuration value can make production print a
  code.
- `POST otp/request` is rate limited to 5 requests / 10 min, partitioned by (IP, hashed
  destination).
- Schema: 9 tables — `users`, `auth_identities`, `auth_sessions`, `otp_codes`, `mfa_secrets`,
  `consent_logs`, `legal_documents`, `legal_acceptances`, `security_audit_logs` — created by the
  `InitialIdentity` migration; `StatusColumnsToText` then moved every status-like column from
  `varchar(n)` to `text` + CHECK, per CONVENTIONS.md.
- SMS login returns 501: no SMS provider is wired up. Email is the only working channel.

## Security & data rules

- Auth is email/SMS OTP — no passwords in MVP. Sessions are an opaque token in an HttpOnly
  cookie; only its hash is stored server-side (not a JWT).
- Roles: `client`, `executor`, `dispatcher`, `qa`, `support`, `finance`, `admin`, `superadmin`.
  MFA is required for every role except `client`/`executor`.
- Session TTL: 14 days for client/executor, 12 hours for privileged roles, with rotation on use.
  Rotation slides the expiry but never past an absolute wall measured from `created_at`:
  30 days for client/executor, 24 hours for privileged roles. A session past its wall is
  revoked server-side and audited as `session_revoked` / `max_lifetime_exceeded`.
- MFA secrets are encrypted with ASP.NET Data Protection. The key ring **must** be persisted
  (`DataProtection__KeyRingPath`, backed by the `dataprotection_keys` volume in Compose) —
  on the framework default it lives on the container's writable layer, so recreating the api
  container silently makes every stored MFA secret undecryptable. Back this volume up
  alongside `postgres_data`.
- Media (photo/video evidence) lives in a private S3-compatible bucket via presigned URLs —
  never on local disk, never served through the API process itself.
- Payments go through YooKassa as one-off redirect-confirmation payments (no Safe Deal/escrow).
  Never trust a webhook body — always re-fetch status from the provider. Writes are idempotent
  via a unique `order_ref` plus an `Idempotence-Key` header. Refunds must be implemented, not
  stubbed.
- Data localization: personal data of RF citizens is stored on RF-hosted infrastructure.
- PII (full name, phone, email, address, coordinates, OTP codes) must never appear in
  free-form logs. Log identifiers and event types, not the values themselves.
- Money is always `numeric(12,2)` decimal — never `float`/`double`, on either side of the wire.
- Executor payout amounts and margins are commercially confidential — they must never leak into
  user-facing content (client-visible order pages, dispute text, notifications).

## Interface quality (`skills-main/`)

`skills-main/skills/` holds a set of interface skills that apply to **every** change touching
`frontend/`. Read the relevant `SKILL.md` before writing UI, not after.

- `better-accessibility` — focus, keyboard, ARIA, hit areas, motion, forms
- `better-layout` — grouping, alignment, reading order, breakpoints, logical properties
- `better-writing` — labels, errors, empty states, capitalization, voice
- `better-typography` — scale, line-height, measure, wrapping, truncation
- `better-colors` — token roles, ramps, measured contrast
- `better-ui` — radius, shadows, icons, motion values
- `better-interface` — coordinates all six for a whole-screen review
- `interface-review` — user-invoked review of a change; never start it unprompted

Rules that decide work here:

- **Escalation triggers are `HIGH` on sight** — a control with no accessible name, a keyboard-
  reachable control with no visible focus ring, content clipped at 320px or 200% zoom, text
  failing its contrast ratio, meaning carried by colour alone, an error naming no way to recover.
- **Evidence, not taste.** Report what you measured, with `path/to/file:line`. Never quote a
  contrast ratio you did not compute or a visual claim you did not inspect in a browser.
- **Prefer the cheaper fix**, in order: delete → use the platform → reuse an existing token →
  correct the value → add something new. A new wrapper where a deletion would do is its own bug.
- **One root cause is one finding.** Fix it at the token or shared component, not per occurrence.
- Domain skills own their rules. Values in them are exact (`scale(0.96)`, not `0.95`); use them
  as written rather than a familiar-looking substitute.

## Commands

Backend (`backend/`):

```bash
dotnet build PamyatRyadom.sln
dotnet test PamyatRyadom.sln     # 106 tests (health + Identity). xUnit + Testcontainers:
                                 # needs a running Docker daemon, otherwise nearly every
                                 # test fails on "cannot connect to the Docker daemon"
dotnet ef migrations add <Name> --project src/PamyatRyadom.Api --startup-project src/PamyatRyadom.Api
```

`dotnet ef database update` needs a connection string, which `appsettings` does not carry. Against
the compose Postgres (published on `POSTGRES_PORT`, default 5442):

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5442;Database=pamyat_ryadom;Username=pamyat_ryadom_user;Password=change_me" \
  dotnet ef database update --project src/PamyatRyadom.Api --startup-project src/PamyatRyadom.Api
```

`bash deploy/migrate.sh` does the same from a one-off SDK container on the compose network, reading
credentials from `.env` — use it on a server where the DB port is not published to the host. The api
container does **not** apply migrations on startup; either command has to be run explicitly.

Frontend (`frontend/`):

```bash
npm run dev
npm run build
npx tsc --noEmit    # type check only; `npm run build` also runs it
npm run lint
npm run test:e2e    # Playwright is configured (testDir ./e2e) but no specs exist yet
```

Stack (repo root):

```bash
cp .env.example .env
docker compose up --build          # dev: postgres + api + frontend + adminer (override auto-loaded)
docker compose down
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build   # production
```

## Definition of Done

A task is done only when it builds (`dotnet build` and `npm run build` both succeed), any DB
change ships as an EF Core migration, Docker Compose still comes up clean, PII/commercial-
confidentiality rules are respected, and relevant docs (`ARCHITECTURE.md`, `CONVENTIONS.md`,
ADRs) are updated if behavior or structure changed.
