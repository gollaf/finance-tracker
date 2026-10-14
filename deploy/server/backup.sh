#!/usr/bin/env bash
# Backs up the database: a pg_dump in PostgreSQL's compressed custom format,
# read back in full to prove it is complete, kept here for KEEP_DAYS days and
# uploaded to the Object Storage bucket in BACKUP_UPLOAD_URL.
#
# Lives next to compose.yaml and .env. finance-tracker-backup.timer runs it
# daily; by hand: bash backup.sh
set -euo pipefail

cd "$(dirname "$0")"

backup_dir=backups
keep_days=7

# Only this one line is read from .env, rather than sourcing the whole file.
upload_url=$(grep -E '^BACKUP_UPLOAD_URL=' .env | cut -d= -f2- || true)

mkdir -p "$backup_dir"
name="financetracker-$(date -u +%Y%m%dT%H%M%SZ).dump"
partial="$backup_dir/$name.partial"

# Written under a temporary name and renamed only once checked, so a failed
# run never leaves a file that looks like a good backup.
trap 'rm -f "$partial"' EXIT

docker compose exec -T postgres \
  pg_dump --username=postgres --dbname=financetracker --format=custom > "$partial"

# Reads every table's data, not just the table of contents, so a truncated
# or corrupt file fails here instead of on the day it is needed.
docker compose exec -T postgres pg_restore --file=/dev/null < "$partial"

mv "$partial" "$backup_dir/$name"
echo "Backup written: $backup_dir/$name ($(du -h "$backup_dir/$name" | cut -f1))"

find "$backup_dir" -name 'financetracker-*.dump' -mtime +"$keep_days" -delete

if [[ -z "$upload_url" ]]; then
  echo "BACKUP_UPLOAD_URL is not set in .env: the backup exists only on this server." >&2
  exit 1
fi

# The URL ends in /o/; the object name completes it. --upload-file sends a PUT.
curl --fail --silent --show-error --retry 3 --upload-file "$backup_dir/$name" "$upload_url$name"
echo "Uploaded to Object Storage: $name"
