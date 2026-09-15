#!/usr/bin/env bash
# Runs exactly once: the official postgres image only executes files under
# /docker-entrypoint-initdb.d/ when $PGDATA is empty, i.e. on the very first start of a
# fresh named volume. It is also written to be safe to re-run by hand (e.g. inside the
# container for recovery) - CREATE ROLE/DATABASE only happen when the object is missing,
# and re-running updates the role's password to match the current APP_DB_PASSWORD.
#
# Creates the non-superuser application role and database that the API/migration
# containers connect as. The PostgreSQL admin/bootstrap user (POSTGRES_USER, default
# "postgres") is never used by the application - only by this script, once.
set -euo pipefail

: "${APP_DB_NAME:?APP_DB_NAME must be set (see deploy/.env)}"
: "${APP_DB_USER:?APP_DB_USER must be set (see deploy/.env)}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD must be set (see deploy/.env)}"
: "${POSTGRES_USER:=postgres}"

# Values are passed to psql as -v variables and substituted as :'name' (a safely
# SQL-string-quoted literal) or via format(%I, %L) below - never string-concatenated by
# this script, so a password containing a quote, backslash or other special character
# cannot break the generated SQL.
#
# Each :'name' reference below is written as a top-level argument to format(...), never
# inside a dollar-quoted ($$...$$) block: psql's :'name' substitution happens as a text
# rewrite of the script BEFORE it reaches the server, and it deliberately does NOT rewrite
# inside dollar-quoted string bodies (that is the point of dollar-quoting - to be opaque to
# psql's own substitution). A DO $do$ ... :'name' ... $do$ block would therefore send the
# literal, unsubstituted text ":'name'" to the PL/pgSQL parser and fail; every :'name' here
# is instead an argument to a plain top-level SELECT ... \gexec, where substitution does
# apply.
psql -v ON_ERROR_STOP=1 \
     --username "$POSTGRES_USER" \
     --dbname postgres \
     -v app_user="$APP_DB_USER" \
     -v app_password="$APP_DB_PASSWORD" \
     -v app_db="$APP_DB_NAME" <<-'EOSQL'
    -- Role missing: create it.
    SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password')
    WHERE NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = :'app_user')
    \gexec

    -- Role already exists (e.g. this script is being re-run): bring its password in line
    -- with the current APP_DB_PASSWORD instead of leaving a stale one in place. Never a
    -- superuser either way - only LOGIN is ever granted.
    SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password')
    WHERE EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = :'app_user')
    \gexec

    -- Database missing: create it, owned by the application role. Never dropped/recreated
    -- if it already exists - this script only ever adds, it does not touch existing data.
    SELECT format('CREATE DATABASE %I OWNER %I', :'app_db', :'app_user')
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'app_db')
    \gexec
EOSQL

echo "Application role/database ready (db=${APP_DB_NAME}, user=${APP_DB_USER})."
