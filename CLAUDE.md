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
- Implemented so far: **Identity**, **BurialSites**, **Catalog**, **Orders/Estimates**, **Media**,
  **Payments** (stub provider), **Dispatch/Visits** (see below). Reports live inside Dispatch;
  Disputes and Subscriptions are unstarted. An order now runs the whole way: priced, paid,
  dispatched, photographed, reviewed and accepted.
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

## Orders module (implemented)

- `OrderStateMachine` (`Models/Orders/OrderStateMachine.cs`) owns the 16-status lifecycle and is
  the only authority on transitions. `OrderService.Move()` is the only place `order.Status` is
  ever written — nothing else assigns to it.
- Internal statuses are an operational vocabulary. `OrderStatusPresentation` maps each to the
  text and single CTA a client sees; never show a raw status to a client.
- `OrderStatuses.QueueGroup()` sorts a status into `staff` / `customer` / `in_flight` / `done`
  and is what the dispatcher queue groups by. A new status must join exactly one group —
  `QueueGroupingTests` fails otherwise, because an ungrouped status would silently vanish from
  the queue.
- An order snapshots the package version it was sold under (code, version, title, price,
  warranty). Editing the catalogue afterwards must never change a sold order.
- Estimates are versioned and acceptance is version-specific (BR-002): publishing a correction
  supersedes the previous version and invalidates any acceptance that was not yet paid.

## Payments module (stub provider)

- `IPaymentProvider` has three operations and deliberately no "mark as paid". A payment becomes
  paid because `GetAsync` said so — never because a callback claimed it. The callback endpoint
  reads the reference and nothing else, then re-fetches (BR-007).
- The provider is chosen **by environment, not configuration**, exactly like the email sender.
  `StubPaymentProvider` throws if constructed in Production; Production registers a provider that
  throws at resolve time until the YooKassa adapter exists, so a build with no way to take money
  fails loudly instead of looking healthy.
- The stub confirms nothing on its own — a payment sits in `waiting_for_capture` until something
  explicitly confirms it, so the code that waits for confirmation is actually exercised. The dev
  endpoint `POST /api/v1/dev/payments/{id}/confirm` marks the stub succeeded and then makes the
  application re-read it; it never shortcuts to "order paid".
- `payments.order_ref` is unique — that index is what makes a retry idempotent rather than a
  double charge. Refunds are implemented, not stubbed; a full refund moves the order to
  `refunded`, a partial one does not (the work still happened).

## Dispatch module (implemented)

- A `Visit` is one executor's trip. The package checklist is **copied onto the visit** at
  assignment: QA measures work against what was promised when it was sold, so a checklist still
  pointing at a live catalogue row would let an edit move that line afterwards.
- Only one live visit per order — two offers out at once means two people at one grave.
- A report is refused unless every checklist line is answered and there is at least one `before`
  and one `after` photo (BR-008). Anything but `done` requires a note, enforced in the service
  **and** by a CHECK constraint.
- QA sits between the executor and the client: `GetReportForClientAsync` returns only an
  `approved` visit, and 404s otherwise (BR-010). Sending work back requires a reason.
- `VisitDto` (staff/executor) carries `PayoutRub`; `VisitReportDto` (client) is a **separate type**
  that does not have the field at all. Not filtered — absent, so no future field can reintroduce
  it. `VisitFlowTests` asserts the figure appears nowhere in the client's response body.
- Media `owner_type=visit`: written only by the assigned executor while the visit is still
  actionable; read by that executor, by reviewing staff, and by the client only once approved.

## Staff surfaces

- `/api/v1/admin/catalog` — admin + superadmin only. Prices and package composition are the
  commercial terms of the contract with every client.
- `/api/v1/admin/orders` — dispatcher as well, since pricing an order is a dispatcher's day job.
- Frontend mirrors this: `app/admin/layout.tsx` admits every staff role, `/admin/queue` is open
  to all of them, and `/admin/catalog` + `/admin/plans` 404 for anyone but an admin. The nav
  hides what a role cannot reach, but the API refuses it regardless.
- Order photos: staff who need to look at a grave to do their job (dispatcher, qa, support,
  admin, superadmin) may **read** `owner_type=order` media; only the client who owns the order
  may write it, and only while the order is still theirs to edit. Finance is excluded on purpose.
- `/admin/qa` — the report review queue, open to qa + dispatcher + admin; approving is qa/admin
  only, so the person who arranged the work is not the only one who can sign it off.
- `/executor` — the executor's own visits. Executors are not staff: every read is scoped to the
  caller in the service, so there is no id that reaches another executor's job.
- Refunds are `finance`/`admin` only. A dispatcher prices work; they do not move money.

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

## Legal & consent

- Operator: АО «Гибкие технологии работы», ИНН 1200018705, ОГРН 1251200001580. Requisites live
  in one place — `frontend/lib/legal/company.ts` — because they appear in four documents and the
  footer, and requisites that disagree between them are worse than requisites in one.
  **КПП and the registered address are still missing** and must be added before production.
- Published document versions are declared in `Services/Legal/LegalDocumentRegistry.cs`; the
  wording lives in `frontend/app/legal/`. **Editing the text means bumping the version in the
  same change** — text changed without a new version silently rewrites what past users are
  recorded as having agreed to. A published version is never edited (BR-016), only superseded.
- The privacy policy and the consent are separate instruments under 152-ФЗ: registration records
  a `LegalAcceptance` for each, plus a `ConsentLog(personal_data)`. Never collapse them into one
  "agreed to everything".
- Registration without consent is refused **server-side** (`legal_not_accepted`), not only by the
  form. Returning users are unaffected — they consented at registration.
- Cookie choices are recorded in `consent_logs` including refusals: "asked and declined" is a
  materially different fact from "never asked", and a value living only in the visitor's browser
  proves nothing.

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

- **A filled button keeps its white label on hover.** Every variant must state `hover:text-*`
  explicitly. A bare Tailwind colour utility is specificity (0,1,0) and loses to the global
  `a:hover { color: var(--accent-deep) }` in `globals.css` (0,1,1) — on a `ButtonLink`, which
  renders an `<a>`, the label then takes the link colour and vanishes into the fill.
  `hover:text-*` is (0,2,0) and wins. Check anchor-rendered variants too: only they hit the
  global rule.
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
dotnet test PamyatRyadom.sln     # 200 tests. xUnit + Testcontainers:
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
npm run test:e2e    # 4 specs in frontend/e2e/ (account-menu, button-hover-contrast,
                     # login-consent, order-photos); needs the stack up (E2E_BASE_URL),
                     # so it only actually runs in CI, not in this sandbox
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
