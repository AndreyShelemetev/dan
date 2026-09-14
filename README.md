# Память рядом (Pamyat Ryadom)

Managed remote cemetery-care service for the Russian market — customers order recurring grave
upkeep (cleaning, flowers, photo/video reports) without visiting in person. This repository holds
the MVP: an ASP.NET Core + PostgreSQL backend and a Next.js frontend, deployed together via Docker
Compose. The architecture deliberately mirrors a sibling project so proven auth/storage/payment
code can be ported across with minimal friction.

## Status

Implemented end to end: **Identity** (passwordless email OTP login, opaque session cookies, role
checks, TOTP MFA enrollment, consent logging), **BurialSites**, **Catalog**, **Orders/Estimates**,
**Media** (S3-compatible presigned uploads/downloads), **Payments** (stub provider — see below),
and **Dispatch/Visits** (visit assignment, checklists, photo reports, QA review). An order now
runs the whole way: priced, paid, dispatched, photographed, reviewed and accepted. The public site
renders design direction A («Тихий сад»).

Payments use a stub provider — the YooKassa adapter isn't wired up yet, so no real money moves.
**Disputes** and **Subscriptions** are unstarted; subscriptions are deliberately scoped to after
launch (see `docs/plan/PLAN.md`). Login is email OTP only — SMS returns `501` from the API and the
login form doesn't offer it.

## Run locally

1. Copy the environment template and adjust as needed:

   ```bash
   cp .env.example .env
   ```

2. Start the stack (Postgres + API + frontend, plus Adminer in dev via the auto-loaded override):

   ```bash
   docker compose up -d --build
   ```

   - Frontend: http://localhost:3100
   - API health check: http://localhost:5100/api/v1/health
   - Swagger (dev only): http://localhost:5100/swagger
   - Adminer (dev only): http://localhost:8090

3. Apply the database migrations — the api container does **not** do this on startup:

   ```bash
   bash deploy/migrate.sh
   ```

4. Tear down:

   ```bash
   docker compose down
   ```

### Signing in locally

Login is passwordless: the backend emails a six-digit code. In `Development` there is no SMTP
server — `ConsoleEmailSender` writes the code to the api container log instead:

```bash
curl -X POST http://localhost:5100/api/v1/auth/otp/request \
  -H 'Content-Type: application/json' \
  -d '{"destination":"you@example.com","channel":"email"}'

docker compose logs api | grep -A2 'DEV EMAIL'    # ← the code is here
```

Verifying the code (`POST /api/v1/auth/otp/verify` with `destination`, `channel` and `code`) sets
the `pamyat_ryadom_auth` session cookie; `GET /api/v1/auth/me` returns the signed-in user and 401
without it. Requesting codes is rate limited to 5 per 10 minutes per IP + address.

For a production deploy, use the explicit override instead of the dev one:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

## Verifying changes

A fresh sandbox (cloud session or a clean checkout) has neither the .NET SDK nor
`frontend/node_modules` installed. One script gets both ready:

```bash
bash scripts/verify-setup.sh
```

It installs the .NET SDK into `/opt/dotnet-sdk` (skipped if `dotnet` is already on `PATH`) and
runs `npm ci` in `frontend/`. After it finishes, the standard checks are:

```bash
export PATH="/opt/dotnet-sdk:$PATH"        # only if the script installed the SDK
dotnet build backend/PamyatRyadom.sln

cd frontend
npx tsc --noEmit
npm run lint
npm run build
```

`dotnet test` additionally needs a running Docker daemon (the test project uses Testcontainers
for Postgres); without one, tests that need a database fail with
`DockerUnavailableException` rather than a real assertion failure.

## Structure

- `backend/` — ASP.NET Core Web API (`PamyatRyadom.Api`) + xUnit test project, EF Core/Npgsql.
- `frontend/` — Next.js App Router frontend.
- `deploy/` — migration, backup, and nginx helpers for a single-VPS deploy.
- `docs/` — project documentation, including `docs/adr/`.

## Documentation

- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — module boundaries, layering, how the stack runs.
- [`CONVENTIONS.md`](./CONVENTIONS.md) — code, schema, and naming conventions.
- [`docs/`](./docs) — product brief and architecture decision records.
