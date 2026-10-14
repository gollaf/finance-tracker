#!/usr/bin/env bash
# Restores a backup made by backup.sh into a database that doesn't exist yet:
#
#   bash restore.sh backups/financetracker-20261015T030000Z.dump financetracker_restored
#
# It refuses an existing database, so it can never overwrite data by mistake.
# Replacing the live database is a deliberate, separate step: see the
# "Backups" section of the README.
set -euo pipefail

cd "$(dirname "$0")"

if [[ $# -ne 2 ]]; then
  echo "Usage: bash restore.sh <backup file> <new database name>" >&2
  exit 2
fi

dump=$1
database=$2

if [[ ! -f "$dump" ]]; then
  echo "No such file: $dump" >&2
  exit 1
fi

# The name ends up inside SQL below; this keeps it a plain identifier.
if [[ ! "$database" =~ ^[a-z_][a-z0-9_]*$ ]]; then
  echo "Database name: lowercase letters, digits and underscores only." >&2
  exit 1
fi

exists=$(docker compose exec -T postgres psql --username=postgres --dbname=postgres \
  --no-psqlrc --tuples-only --no-align \
  --command="SELECT 1 FROM pg_database WHERE datname = '$database'")

if [[ -n "$exists" ]]; then
  echo "Database '$database' already exists; refusing to overwrite it." >&2
  exit 1
fi

docker compose exec -T postgres createdb --username=postgres "$database"

# --exit-on-error: stop at the first problem rather than leave a partly
# restored database that looks fine.
docker compose exec -T postgres \
  pg_restore --username=postgres --dbname="$database" --exit-on-error < "$dump"

echo "Restored $dump into database '$database'."
