# Production deployment (Docker Compose)

This packages the kiosk for an Ubuntu/Linux VPS: Nginx serving the built frontend and
proxying a small allowlist of API routes to the ASP.NET Core backend, PostgreSQL with a
persistent volume, and a one-shot migration step. It does **not** connect to any real
server by itself and ships no real credentials.

Public HTTPS comes from a reverse proxy that already runs on the VPS in its own container
(the QRLog stack's Caddy), reached over a shared Docker network - see §17 and §18.

## 1. Prerequisites

- Docker Engine with the Compose plugin (`docker compose`, not the old standalone
  `docker-compose`)
- Enough disk for the PostgreSQL volume and Docker's image cache

## 2. Create your `.env`

```bash
cp deploy/.env.example deploy/.env
```

Then replace every `CHANGE_ME_...` placeholder in `deploy/.env` with a long, random,
unique value. Example generator for a single password:

```bash
openssl rand -base64 32
```

`deploy/.env` is git-ignored (`deploy/.env.example` is not) - never commit your real one.

## 3. Validate the config

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml config
```

If any required variable in `deploy/.env` is missing or empty, this fails immediately with
a clear message naming the variable (every secret in `compose.yml` uses the
`${VARIABLE:?error message}` syntax) - it does not silently start with an empty password.

`SHARED_PROXY_NETWORK` must name a network that **already exists** (on the production VPS:
`attendanceqr_default`). Compose never creates it; check first with
`docker network inspect attendanceqr_default`.

## 4. Build

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml build
```

## 5. Start

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml up -d
```

Startup order is enforced by health/completion conditions: `db` becomes healthy, then
`migrate` runs and must exit `0`, then `api` starts (and must become healthy), then `web`
starts.

## 6. Check the migration step succeeded

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml ps migrate
docker compose --env-file deploy/.env -f deploy/compose.yml logs migrate
```

`ps` should show it `Exited (0)`. If it exited non-zero, `api` never starts (Compose's
`service_completed_successfully` condition blocks it) - check `logs migrate` for the
reason before doing anything else.

## 7. Logs

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml logs -f            # all services
docker compose --env-file deploy/.env -f deploy/compose.yml logs -f api        # one service
```

Every service's logs are capped (`json-file`, 10m × 3 files per service) so they cannot
silently fill the VPS disk.

## 8. Health check

```bash
curl -f http://127.0.0.1:8080/health   # or your APP_HTTP_PORT from deploy/.env, if you changed it
```

This reaches Nginx, which proxies to the API's `/health` endpoint (a real PostgreSQL
connectivity check - 200 when the database is reachable, 503 when it is not; the body
never contains an exception, connection string, host or credential, only a short stable
status).

## 9. Stop / restart

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml stop      # stop, keep containers+data
docker compose --env-file deploy/.env -f deploy/compose.yml down      # remove containers, KEEP the named volume (data survives)
docker compose --env-file deploy/.env -f deploy/compose.yml up -d     # start again
```

## 10. Backup

```bash
deploy/scripts/backup-postgres.sh
```

Writes a timestamped `pg_dump` (custom format) of the application database to
`deploy/backups/` (git-ignored - backups never get committed). The script only ever talks
to *this* project's own `db` service (`--project-name literature-millionaire`); it will
refuse to run if that service is not currently running.

Automated/scheduled backups (cron, retention policy) are Task 11B.2's job - run this
script manually for now.

## 11. Restoring a backup - READ THIS FIRST

**There is no restore script on purpose.** Restoring overwrites live data and must be a
deliberate, reviewed action, not a one-liner someone runs on autopilot. If you actually
need to restore:

```bash
# ⚠ DESTRUCTIVE. This replaces the CURRENT contents of the application database. Take a
# fresh backup-postgres.sh backup first if you might need to undo this.
docker compose --env-file deploy/.env -f deploy/compose.yml exec -T db \
  sh -ec 'PGPASSWORD="$APP_DB_PASSWORD" pg_restore --clean --if-exists --no-owner --username "$APP_DB_USER" --dbname "$APP_DB_NAME"' \
  < deploy/backups/the-file-you-want.dump
```

`$APP_DB_USER`/`$APP_DB_NAME`/`$APP_DB_PASSWORD` inside the single-quoted `sh -ec '...'` script are expanded by a shell *inside the `db` container*, against the environment Compose already gave that container from `deploy/.env` - not by your host shell (which never has those variables unless you explicitly exported them yourself, and the command above does not rely on that). The password is never written to your terminal or shell history.

Stop the `api` service first if you want to be certain nothing writes to the database
while you restore.

## 12. Clean install (⚠ deletes all data)

```bash
docker compose --env-file deploy/.env -f deploy/compose.yml down -v
docker compose --env-file deploy/.env -f deploy/compose.yml up -d
```

**`down -v` permanently deletes the named PostgreSQL volume - every participant, attempt
and leaderboard row is gone, unresolvable, not "in the trash".** This is not a routine
command; use plain `down` or `stop` for every normal stop/restart (see §9). Only reach for
`down -v` when you deliberately want a fresh, empty database (e.g. tearing down a disposable
test stack).

## 13. Where your data actually lives

PostgreSQL's data directory is the named volume `postgres-data` (Docker manages its files
under `/var/lib/docker/volumes/...`; you do not need to touch that path directly). It
survives `docker compose down`, `stop`, container recreation, image rebuilds and host
reboots - it is only ever destroyed by `down -v` or an explicit `docker volume rm`.

## 14. Changing `.env` after the volume already exists

Editing `deploy/.env` and restarting the stack does **not** re-run
`deploy/db/init/01-init-app-role-and-db.sh` - PostgreSQL's official image only executes
files under `/docker-entrypoint-initdb.d/` the very first time the volume is initialized
(i.e. when it's empty). So:

- Changing `APP_DB_NAME`/`APP_DB_USER` after the first start does **not** rename or
  recreate anything - the API will simply fail to connect (wrong name/user for the
  existing database/role).
- Changing `APP_DB_PASSWORD` or `POSTGRES_PASSWORD` after the first start does **not**
  change the password PostgreSQL actually has stored for that role.

Rotating a credential on an existing volume is a separate, deliberate action (e.g.
`ALTER ROLE ... PASSWORD ...` executed against the running database, followed by updating
`deploy/.env` to match) - not something this task automates. Treat it the same way you'd
treat any other production credential rotation.

## 15. Only Nginx is public

Only the `web` (Nginx) service is reachable from outside this stack, and only two ways:

- over the shared reverse-proxy network as `literature-millionaire-web:80` (§17), and
- on the host's loopback, `127.0.0.1:${APP_HTTP_PORT:-8080} → 80`, for verification and
  diagnostics on the server itself.

`db`, `migrate` and `api` publish **no** host port and are **not** on the shared network -
they are reachable only from other containers of this project on the `internal` network.
Nothing about PostgreSQL or the raw API is exposed to the internet, to other projects on
the VPS, or to the host's other processes.

## 16. Admin/CRUD endpoints are blocked at Nginx, not just "not linked to"

The project has no authentication yet. Because of that, `frontend/nginx.conf` runs a
strict **public API allowlist** - only the five routes the kiosk itself calls are proxied
through:

- `GET /api/campaigns/current`
- `GET /api/campaigns/{numeric id}/leaderboard`
- `POST /api/game/start`
- `POST /api/game/{GUID}/answer`
- `POST /api/game/{GUID}/timeout`

Every other `/api/...` path - Question CRUD, Book CRUD, `/api/game/prizes` (the legacy
prize-ladder endpoint), anything else - returns a plain 404 from Nginx itself, the request
never reaches the `api` container. The frontend's `/admin/questions` route is blocked the
same way (`/admin` → 404 from Nginx, never `index.html`), and the wrong HTTP method on an
allowlisted path (e.g. `GET /api/game/start`) also 404s rather than reaching the backend.

**This is not authentication** - it is a stop-gap that keeps the public deployment surface
limited to kiosk endpoints until real auth exists. Anyone who can reach the Docker network
directly (i.e. anyone with shell access to the VPS) can still reach the full API. Do not
present this as a security boundary beyond "the public internet only sees the kiosk
routes".

## 17. Shared reverse-proxy network

The production VPS has no host Nginx or Caddy. Ports 80/443 belong to the Caddy container of
the QRLog stack, which issues and renews certificates itself. A proxy inside a container
cannot reach `127.0.0.1` on the host, so Caddy reaches this project over a Docker network it
is already on:

| | |
|---|---|
| `SHARED_PROXY_NETWORK` on the VPS | `attendanceqr_default` (declared `external: true` - must already exist) |
| Caddy upstream for this project | `literature-millionaire-web:80` |
| On the shared network | `web` only |
| On the `internal` network only | `api` (alias `literature-millionaire-api`), `migrate`, `db` |

Both aliases are project-unique on purpose. The shared network already carries several
containers named generically (`web`, `db`, `app`), and a name collision there once made
another project connect to the wrong database. The web container's Nginx therefore proxies
to `literature-millionaire-api`, which exists only on `internal`, never to a bare `api`.

Compose also registers each service's own name (`web`) as an alias on every network it
joins, the same as the other projects already on that network. Nothing should ever target
that generic name - only `literature-millionaire-web`.

## 18. Public domain and where the Caddy config lives

The public domain for this project is **`book.qrlog.az`**. It is deliberately **not** in the
Docker images, the frontend bundle or `compose.yml`: the frontend calls same-origin `/api`,
so the same images work on any hostname. The domain appears only in DNS and in the Caddy
site block.

That site block does **not** belong to this repository. The Caddyfile is tracked in the
**QRLog repository** and bind-mounted into its Caddy container, and QRLog's deploy resets its
working tree - a block typed into the Caddyfile on the server is silently deleted by the next
QRLog deploy. Every Caddy change for this project must therefore be committed to the QRLog
repository, validated, and only then reloaded; never edited in place on the server.
