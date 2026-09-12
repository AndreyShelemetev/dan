# Ops runbook

## Health signals

- **Liveness — `GET /api/v1/health`.** Process is up, nothing else. Never restart a container
  on this alone; it checks no dependency on purpose (`Controllers/HealthController.cs`).
- **Readiness — `GET /api/v1/health/ready`.** Postgres answers *and* the schema has every
  migration this build expects. 200 = ready; 503 with a JSON `reason` otherwise:
  - `database_unreachable` — Postgres didn't answer `CanConnectAsync`.
  - `pending_migrations` — connected, but the schema is behind this build.
  - `migration_check_failed` — connected, but the migrations-table query itself failed
    (permissions, corrupt history table) — fails closed rather than reporting ready on an
    incomplete check.
  This is what `docker-compose.yml`'s `api` healthcheck watches, and what any future load
  balancer or process supervisor should watch too — "healthy" used to mean only "the process
  started", which is why a fresh, unmigrated volume used to look healthy while every data
  endpoint 500ed (see `ARCHITECTURE.md` §6).
- `docker compose ps` shows each container's health at a glance; for the readiness reason on a
  red `api`, just curl it — `docker exec pamyat-ryadom-api curl -fsS localhost:8080/api/v1/health/ready`.

## Log aggregation

- Both `api` and `frontend` log to stdout/stderr; Docker's default `json-file` driver captures
  it under `/var/lib/docker/containers/<id>/*-json.log`. Tail live with `docker compose logs -f
  api` (or `frontend`, `postgres`).
- **Cap disk usage.** The default `json-file` driver has no size limit — on a long-lived VPS
  that is an unbounded disk leak. Add to whichever compose file(s) run in production, or to
  `/etc/docker/daemon.json` for every container on the host:
  ```yaml
  logging:
    driver: json-file
    options:
      max-size: "10m"
      max-file: "5"
  ```
- For retention beyond a few days, ship the same json-file logs off-host with a lightweight
  shipper (`vector`, `promtail` → Loki, or a cron job doing `docker compose logs --since 24h`
  plus `logrotate`). Picking and standing up a shipper is ops work for whoever runs the deploy,
  not something wired up here.
- **Never enable EF Core sensitive-data logging in Production** — it prints parameter values
  (names, phones, addresses, coordinates) straight into these logs, which is exactly what
  CLAUDE.md's PII rule forbids. There is no toggle for it in `Program.cs` today; keep it that
  way, and treat a PR that adds one as a regression, not a debugging convenience.

## Alerts

Two signals worth alerting on, both derivable from what already exists in the repo. Wiring
either into a paging/notification channel (email, Telegram, a monitoring SaaS — whatever the
operator already uses) is left to whoever runs the deploy; this runbook only says what to watch
and what it means once it fires.

- **Readiness healthcheck failing.** `docker inspect --format='{{.State.Health.Status}}'
  pamyat-ryadom-api`. Anything other than `healthy` for longer than the healthcheck's own
  `retries × interval` (3 × 30s ≈ 90s, see `docker-compose.yml`) means Postgres is unreachable
  or a migration is pending — the JSON body's `reason` (above) says which.
- **5xx rate on the reverse proxy.** nginx's access log already records the response status;
  alert when the share of 5xx responses over a short rolling window crosses a threshold (a
  starting point, not a tuned value — e.g. more than 5% of the last 100 requests):
  ```bash
  tail -n 100 /var/log/nginx/access.log | awk '{print $9}' | grep -c '^5'
  ```

## What to do when

### `api` is red

1. `docker compose logs --tail 200 api` — look for a stack trace or exception first.
2. `curl -fsS http://localhost:${API_PORT}/api/v1/health` (liveness). Not 200 → the process
   itself crashed or never started; the exception in step 1 is almost always a startup config
   problem (missing env var, bad connection string, Data Protection key ring path unwritable).
3. Liveness OK but `curl -fsS http://localhost:${API_PORT}/api/v1/health/ready` is 503 → the
   body's `reason` says which of the three cases above applies; `pending_migrations` is fixed
   by `bash deploy/migrate.sh`, the other two point at Postgres (below).

### The database is not responding

1. `docker compose ps postgres` — is the container even running and healthy?
2. `docker compose logs --tail 200 postgres` — out of disk, out of connections, crash-looped?
3. `docker exec pamyat-ryadom-postgres pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"`.
4. If the volume itself is corrupted or lost, restore from the latest backup —
   `docs/ops/backup-restore.md`.

### S3 (the media bucket) is not serving photos/video

1. Confirm the endpoint from the api's own point of view:
   `docker exec pamyat-ryadom-api env | grep Storage__` — a presigned URL signed against the
   wrong `Storage__PublicEndpoint` 403s at the bucket, not at the api, so the api's own logs
   stay quiet.
2. Check the configured provider's own status page for the region (Yandex Object Storage /
   Selectel / VK Cloud — whichever `.env` points at).
3. This is deliberately not part of readiness — a media outage degrades photo upload/viewing,
   it does not take down the rest of the app, so `/health/ready` staying green during an S3
   outage is expected, not a bug.

### Payments are not going through (YooKassa)

1. `docker compose logs api | grep -i payment` — per CLAUDE.md's logging rule this should carry
   only identifiers and status codes, no PII or secrets; search by the order's own `order_ref`.
2. Re-check via `GET /api/v1/orders/{id}` rather than trusting a stale UI state — a payment is
   only ever marked paid because `IPaymentProvider.GetAsync` said so on a fresh call, never
   because a webhook claimed it (BR-007), so a provider outage shows up as the order staying
   unpaid on re-fetch, not as an inconsistent status.
3. Check YooKassa's own status page — there is no fallback provider to fail over to.
