#!/usr/bin/env bash
# Runs exactly once: the official postgres image only executes files under
# /docker-entrypoint-initdb.d/ when $PGDATA is empty, i.e. on the very first start of a
# fresh named volume. It is also written to be safe to re-run by hand (e.g. inside the
# container for recovery) - CREATE ROLE/DATABASE only happen when the object is missing.
#
# Creates the non-superuser application role and database that the API/migration
# containers connect as. The PostgreSQL admin/bootstrap user (POSTGRES_USER, default
# "postgres") is never used by the application - only by this script, once.
set -euo pipefail

: "${APP_DB_NAME:?APP_DB_NAME must be set (see deploy/.env)}"
: "${APP_DB_USER:?APP_DB_USER must be set (see deploy/.env)}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD must be set (see deploy/.env)}"
: "${POSTGRES_USER:=postgres}"

# Passwords/identifiers are passed to psql as variables (-v) and referenced as :'name' in
# the SQL below, never concatenated into the query text ourselves - psql performs the
# SQL-string quoting (safe against a password containing a quote, backslash, etc.), and the
# DO block below uses format(..., %I, %L) for the identifier/literal that CREATE ROLE
# cannot take as a bind parameter directly.
psql -v ON_ERROR_STOP=1 \
     --username "$POSTGRES_USER" \
     --dbname postgres \
     -v app_user="$APP_DB_USER" \
     -v app_password="$APP_DB_PASSWORD" \
     -v app_db="$APP_DB_NAME" <<-'EOSQL'
    DO
    $do$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = :'app_user') THEN
            EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password');
        ELSE
            EXECUTE format('ALTER ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password');
        END IF;
    END
    $do$;

    -- CREATE DATABASE cannot run inside a DO block/transaction, so this generates the
    -- statement as text (only when the database does not exist yet) and \gexec runs it.
    SELECT format('CREATE DATABASE %I OWNER %I', :'app_db', :'app_user')
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'app_db')\gexec
EOSQL

echo "Application role/database ready (db=${APP_DB_NAME}, user=${APP_DB_USER})."
