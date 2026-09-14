#!/usr/bin/env bash
# Pamyat Ryadom Data Protection key ring backup — archives the api container's
# dataprotection_keys volume to a gzip tarball and prunes old backups. Designed
# to run from cron, right alongside backup-db.sh.
#
# Losing this volume without a backup makes every stored MFA secret permanently
# undecryptable (see CLAUDE.md's Data Protection rule) — it does not fail loudly
# on its own, so back it up on the same schedule as the database.
#
#   Usage:  /opt/pamyat-ryadom/deploy/backup-keys.sh
#   Env:    BACKUP_DIR      (default /opt/pamyat-ryadom/backups/keys)
#           RETENTION_DAYS  (default 14)
#           CONTAINER       (default pamyat-ryadom-api)
#           KEY_RING_PATH   (default /var/lib/pamyat-ryadom/dataprotection-keys —
#                            must match DataProtection__KeyRingPath in docker-compose.yml)
set -euo pipefail
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${PATH:-}"

BACKUP_DIR="${BACKUP_DIR:-/opt/pamyat-ryadom/backups/keys}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
CONTAINER="${CONTAINER:-pamyat-ryadom-api}"
KEY_RING_PATH="${KEY_RING_PATH:-/var/lib/pamyat-ryadom/dataprotection-keys}"

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="$BACKUP_DIR/dataprotection-keys-$STAMP.tar.gz"

# Read the volume through the running api container rather than addressing the
# named volume directly — the volume's actual name depends on the compose
# project name, while the container name (and mount path) is fixed by
# docker-compose.yml regardless of project name.
docker run --rm --volumes-from "$CONTAINER" alpine \
  tar czf - -C "$KEY_RING_PATH" . > "$FILE"

# Fail loudly if the archive looks empty — a healthy key ring always has at
# least one XML key file, even before any MFA secret has ever been enrolled.
SIZE="$(stat -c%s "$FILE" 2>/dev/null || stat -f%z "$FILE")"
if [ "${SIZE:-0}" -lt 100 ]; then
  echo "ERROR: key ring backup looks empty ($SIZE bytes): $FILE" >&2
  exit 1
fi

echo "Backup OK: $FILE ($(du -h "$FILE" | cut -f1))"

# Retention: remove archives older than RETENTION_DAYS.
find "$BACKUP_DIR" -name 'dataprotection-keys-*.tar.gz' -type f -mtime "+$RETENTION_DAYS" -delete
