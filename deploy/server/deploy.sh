#!/usr/bin/env bash
# Deploys one published build: backs up the database, switches IMAGE_TAG in
# .env, restarts the services (migrations run first) and waits until the Api
# reports ready. If it doesn't, it switches back to the previous tag.
#
# The GitHub Actions deploy key can run only this script (a forced command in
# ~/.ssh/authorized_keys); the tag it asks for arrives in SSH_ORIGINAL_COMMAND.
# By hand: bash deploy.sh sha-1234567
set -euo pipefail

cd "$(dirname "$0")"

tag=${SSH_ORIGINAL_COMMAND:-${1:-}}

# The tag is the only input from outside, and it ends up in .env and in
# image names: nothing but the exact shape CI publishes is accepted.
if [[ ! "$tag" =~ ^sha-[0-9a-f]{7}$ ]]; then
  echo "Expected an image tag like sha-1234567, got '$tag'." >&2
  exit 2
fi

# One deploy at a time, even if two are started by hand.
exec 9> .deploy.lock
if ! flock --nonblock 9; then
  echo "Another deploy is running." >&2
  exit 1
fi

previous=$(grep -E '^IMAGE_TAG=' .env | cut -d= -f2-)
echo "Deploying $tag (running now: $previous)."

set_tag() {
  sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$1/" .env
}

wait_until_ready() {
  for _ in $(seq 1 30); do
    if curl --fail --silent --max-time 5 http://127.0.0.1:5000/health/ready > /dev/null \
      && [[ -n "$(docker compose ps --quiet --status running worker)" ]]; then
      return 0
    fi
    sleep 3
  done
  return 1
}

# Migrations may change the schema, so the data is saved first. A failed
# backup stops the deploy before anything has changed.
bash backup.sh

# Pulled while the old version keeps running: a variable in the environment
# takes precedence over .env, which stays untouched until the pull succeeded.
IMAGE_TAG=$tag docker compose pull --quiet migrate api worker

set_tag "$tag"
# `up` exits non-zero when migrate fails; the rollback below still runs.
if docker compose up --detach --remove-orphans && wait_until_ready; then
  echo "Deployed $tag: the Api is ready and the Worker is running."
  exit 0
fi

echo "$tag did not become ready; rolling back to $previous." >&2
docker compose logs --tail 50 migrate api worker >&2 || true
set_tag "$previous"
docker compose up --detach --remove-orphans
if wait_until_ready; then
  echo "Rolled back to $previous. The backup taken before this deploy is in backups/." >&2
else
  echo "Rollback to $previous is not ready either: the server needs attention." >&2
fi
exit 1
