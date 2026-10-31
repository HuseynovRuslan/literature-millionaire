#!/usr/bin/env bash
# Archives the uploaded pictures (the "uploads" volume) to
# deploy/backups/uploads-<UTC timestamp>.tar.gz.
#
# Usage: deploy/scripts/backup-uploads.sh
#
# The database backup (backup-postgres.sh) keeps the rows that point at these files; without the
# files themselves, a restored database would show questions with missing pictures. Runs from the
# same cron entry as the database backup.
#
# Nothing here writes to the volume: the archive is made from the web container, which mounts it
# read-only. There is no restore script on purpose - restoring is a deliberate, manual action
# (unpack the archive into the volume while the stack is down).
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
DEPLOY_DIR="$(cd -- "$SCRIPT_DIR/.." >/dev/null 2>&1 && pwd)"
COMPOSE_FILE="$DEPLOY_DIR/compose.yml"
ENV_FILE="$DEPLOY_DIR/.env"
BACKUP_DIR="$DEPLOY_DIR/backups"
PROJECT_NAME="literature-millionaire"

if [ ! -f "$ENV_FILE" ]; then
    echo "Missing $ENV_FILE - copy deploy/.env.example to deploy/.env first." >&2
    exit 1
fi

compose() {
    docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

if [ "$(compose ps --status running --services 2>/dev/null | grep -cx web || true)" -ne 1 ]; then
    echo "The '$PROJECT_NAME' project's 'web' service is not running - start the stack first (docker compose ... up -d)." >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
out_file="$BACKUP_DIR/uploads-${timestamp}.tar.gz"

# -T: no TTY, so the archive arrives on stdout unchanged.
compose exec -T web tar czf - -C /usr/share/nginx/uploads . > "$out_file"

echo "Uploads archive written to: $out_file"
