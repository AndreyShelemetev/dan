# Backup and restore

Two things need backing up, on the same schedule, because losing either one breaks the
service in a different way:

- **`postgres_data`** — every row in the system. `deploy/backup-db.sh` dumps it.
- **`dataprotection_keys`** — the ASP.NET Data Protection key ring `MfaService` uses to
  encrypt TOTP secrets (`CLAUDE.md`, Security & data rules). Restoring the database without
  it does not lose data, but it makes every already-enrolled MFA secret permanently
  undecryptable — every privileged user (every role but `client`/`executor`, per CLAUDE.md)
  has to re-enroll MFA before they can sign in again. `deploy/backup-keys.sh` archives it.

Both scripts run against the container names `docker-compose.yml` assigns
(`${STACK:-pamyat-ryadom}-postgres`, `${STACK:-pamyat-ryadom}-api`), so run them from the host
that runs the stack, with the stack up.

## Schedule (cron)

Add both to the deploy host's crontab (`crontab -e`), staggered so they do not compete for
disk I/O, with `BACKUP_DIR` pointed at a filesystem that is *not* the Docker volume being
backed up — otherwise losing the disk loses the backups with it:

```cron
# Pamyat Ryadom — nightly backups
15 3 * * * BACKUP_DIR=/opt/pamyat-ryadom/backups     /opt/pamyat-ryadom/deploy/backup-db.sh   >> /var/log/pamyat-ryadom-backup-db.log 2>&1
30 3 * * * BACKUP_DIR=/opt/pamyat-ryadom/backups/keys /opt/pamyat-ryadom/deploy/backup-keys.sh >> /var/log/pamyat-ryadom-backup-keys.log 2>&1
```

Both scripts exit non-zero on a suspiciously small/empty backup — wire the cron job's exit
status into whatever the host already alerts on (see `docs/ops/runbook.md`'s alerting
section), so a silent backup failure does not go unnoticed until the day it is needed.

### Retention

Both scripts prune their own output — `RETENTION_DAYS` (default 14) is applied with
`find -mtime`, run at the end of every invocation. Copy backups older than that off-host
(object storage, another machine) before relying on a retention window longer than 14 days;
the scripts only ever look at local disk.

## Restoring the database

Run on the deploy host, with the stack up (`docker compose -f docker-compose.yml
-f docker-compose.prod.yml up -d`):

```bash
# 1. Stop the api so nothing writes while the database is being replaced.
docker compose -f docker-compose.yml -f docker-compose.prod.yml stop api

# 2. Drop and recreate the database, then load the dump. Reads POSTGRES_* from .env,
#    same as deploy/migrate.sh.
CONTAINER="${STACK:-pamyat-ryadom}-postgres"
docker exec "$CONTAINER" psql -U "$POSTGRES_USER" -d postgres \
  -c "DROP DATABASE IF EXISTS $POSTGRES_DB;" \
  -c "CREATE DATABASE $POSTGRES_DB OWNER $POSTGRES_USER;"
gunzip -c /opt/pamyat-ryadom/backups/pamyat-ryadom-<STAMP>.sql.gz \
  | docker exec -i "$CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"

# 3. Bring the api back and let it verify readiness before trusting it (see D23's
#    /api/v1/health readiness check).
docker compose -f docker-compose.yml -f docker-compose.prod.yml start api
```

**Verify the restore succeeded:** `curl -fsS http://localhost:${API_PORT}/api/v1/health` returns
readiness OK, and a known order/user from before the backup is visible again
(`GET /api/v1/admin/orders` or similar, as a staff account). An empty or 404-everywhere
response means the dump did not load — check step 2's `psql` output for errors before moving on.

## Restoring the Data Protection key ring

The key ring is only ever read by the api container at startup, so it is safe to replace
while the api is stopped:

```bash
CONTAINER="${STACK:-pamyat-ryadom}-api"

# 1. Stop the api — do not remove the container, --volumes-from below needs it to still exist.
docker compose -f docker-compose.yml -f docker-compose.prod.yml stop api

# 2. Sanity-check the archive before touching anything live.
tar tzf /opt/pamyat-ryadom/backups/keys/dataprotection-keys-<STAMP>.tar.gz | head

# 3. Replace the volume's contents through the stopped container, then restart it.
docker run --rm --volumes-from "$CONTAINER" \
  -v /opt/pamyat-ryadom/backups/keys:/backup \
  alpine sh -c '
    rm -rf /var/lib/pamyat-ryadom/dataprotection-keys/* &&
    tar xzf /backup/dataprotection-keys-<STAMP>.tar.gz -C /var/lib/pamyat-ryadom/dataprotection-keys
  '
docker compose -f docker-compose.yml -f docker-compose.prod.yml start api
```

**Verify the restore succeeded:** sign in as a privileged user who had MFA enrolled *before*
the backup was taken and confirm their existing authenticator code still passes
`POST /api/v1/auth/mfa/verify`. If it fails with a decryption error where it used to succeed,
the wrong archive was restored (or the ring was regenerated after the backup was taken,
before restore) — restoring an *empty* or missing ring does not error on its own, it just
silently makes every existing secret unusable, so this check is the only real signal that
the restore worked.

## Restoring both together

Restore the database first, then the key ring, then bring the api up once — a database
restored from before an MFA enrollment paired with a key ring restored from after it (or vice
versa) leaves `mfa_secrets` rows that no longer decrypt with the ring in place. Taking both
backups in the same cron run (as scheduled above) keeps them from drifting apart in the first
place.
