#!/usr/bin/env bash
# The restore drill: proves a backup can actually be restored. It restores
# the backup into a scratch database, compares that with the live database,
# and drops the scratch database again. The live database is only read.
#
#   bash restore-drill.sh          # the newest file in backups/
#   bash restore-drill.sh <file>   # a given one, e.g. downloaded from the bucket
set -euo pipefail

cd "$(dirname "$0")"

# The names hold a UTC timestamp, so sorting them sorts by age. A missing
# backups folder must not end the script before the message below.
dump=${1:-$( (find backups -name 'financetracker-*.dump' 2>/dev/null || true) | sort | tail -n 1)}
if [[ -z "$dump" ]]; then
  echo "No backup found in backups/." >&2
  exit 1
fi

scratch=financetracker_restore_drill

sql() {
  docker compose exec -T postgres psql --username=postgres --dbname="$1" \
    --no-psqlrc --tuples-only --no-align --field-separator=' ' --command="$2"
}

# Exact row counts for every table, one "table count" line each.
counts_query="SELECT table_name,
  (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text
  FROM information_schema.tables
  WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
  ORDER BY table_name"
migration_query='SELECT max("MigrationId") FROM "__EFMigrationsHistory"'

drop_scratch() {
  docker compose exec -T postgres dropdb --username=postgres --if-exists "$scratch"
}

# Also removes a scratch database left behind by an interrupted earlier run.
drop_scratch
trap drop_scratch EXIT

bash restore.sh "$dump" "$scratch"

live_counts=$(sql financetracker "$counts_query")
restored_counts=$(sql "$scratch" "$counts_query")
live_migration=$(sql financetracker "$migration_query")
restored_migration=$(sql "$scratch" "$migration_query")

echo
echo "Rows per table, live now and in the backup:"
comparison=$(awk '
  NR == FNR { backup[$1] = $2; next }
  { print $1, $2, ($1 in backup ? backup[$1] : "missing"); delete backup[$1] }
  END { for (table in backup) print table, "missing", backup[table] }
' <(echo "$restored_counts") <(echo "$live_counts"))
awk '{ printf "  %-32s %10s %10s\n", $1, $2, $3 }' <<< "table live backup
$comparison"

echo
echo "Latest migration, live:   $live_migration"
echo "Latest migration, backup: $restored_migration"
echo

# Row counts may differ: rows added since the backup was taken are only live.
# The tables and the schema version must match.
if grep -q missing <<< "$comparison" || [[ "$live_migration" != "$restored_migration" ]]; then
  echo "Drill FAILED: the backup's tables or schema version differ from the live database." >&2
  exit 1
fi

echo "Drill passed: $dump restores completely."
