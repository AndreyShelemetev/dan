#!/usr/bin/env bash
# Pamyat Ryadom PostgreSQL backup — dumps the running postgres container to a
# gzip file and prunes old backups. Designed to run from cron.
#
#   Usage:  /opt/pamyat-ryadom/deploy/backup-db.sh
#   Env:    BACKUP_DIR      (default /opt/pamyat-ryadom/backups)
#           RETENTION_DAYS  (default 14)
#           CONTAINER       (default pamyat-ryadom-postgres)
set -euo pipefail
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${PATH:-}"

BACKUP_DIR="${BACKUP_DIR:-/opt/pamyat-ryadom/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
CONTAINER="${CONTAINER:-pamyat-ryadom-postgres}"

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="$BACKUP_DIR/pamyat-ryadom-$STAMP.sql.gz"

# pg_dump inside the container; credentials come from the container's env.
docker exec "$CONTAINER" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --no-privileges' \
  | gzip > "$FILE"

# Fail loudly if the dump is suspiciously small (e.g. auth error produced an empty file).
SIZE="$(stat -c%s "$FILE" 2>/dev/null || stat -f%z "$FILE")"
if [ "${SIZE:-0}" -lt 1000 ]; then
  echo "ERROR: backup looks empty ($SIZE bytes): $FILE" >&2
  exit 1
fi

echo "Backup OK: $FILE ($(du -h "$FILE" | cut -f1))"

# Retention: remove dumps older than RETENTION_DAYS.
find "$BACKUP_DIR" -name 'pamyat-ryadom-*.sql.gz' -type f -mtime "+$RETENTION_DAYS" -delete
