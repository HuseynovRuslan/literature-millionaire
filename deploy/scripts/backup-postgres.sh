#!/usr/bin/env bash
# Dumps the application database from THIS project's running "db" service only, in
# pg_dump's custom format, to deploy/backups/<db-name>-<UTC timestamp>.dump.
#
# Usage: deploy/scripts/backup-postgres.sh
#
# There is no matching restore script on purpose: restoring is a destructive operation and
# must be a deliberate, reviewed, manual action. See deploy/README.md for a restore example
# and its warnings.
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

# Only used to name the file and to authenticate pg_dump inside the container; never
# printed, logged or passed as a command-line argument (PGPASSWORD is an environment
# variable local to the one `docker compose exec` call below).
set -a
# shellcheck source=/dev/null
source "$ENV_FILE"
set +a

: "${APP_DB_NAME:?APP_DB_NAME must be set in $ENV_FILE}"
: "${APP_DB_USER:?APP_DB_USER must be set in $ENV_FILE}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD must be set in $ENV_FILE}"

compose() {
    docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

if [ "$(compose ps --status running --services 2>/dev/null | grep -cx db || true)" -ne 1 ]; then
    echo "The '$PROJECT_NAME' project's 'db' service is not running - start the stack first (docker compose ... up -d)." >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
out_file="$BACKUP_DIR/${APP_DB_NAME}-${timestamp}.dump"

compose exec -T -e PGPASSWORD="$APP_DB_PASSWORD" db \
    pg_dump --format=custom --no-password --username "$APP_DB_USER" --dbname "$APP_DB_NAME" \
    > "$out_file"

echo "Backup written to: $out_file"
