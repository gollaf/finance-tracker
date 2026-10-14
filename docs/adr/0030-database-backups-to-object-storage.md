# 30. Database backups to Object Storage

## Status

Accepted

## Context

The public demo (ADR 0029) keeps all its data in one PostgreSQL database
on one VM (ADR 0025), on a Docker volume. Nothing protects it: a deleted
volume, a broken disk, a reclaimed instance or a bad migration would lose
every account, transaction and user. Strangers can now register, so the
data is no longer only the author's.

Everything is in that one database: the domain data, the outbox, the
Identity users and the Data Protection keys that sign-in tokens depend on.
RabbitMQ holds only messages in flight, which the outbox can resend.

The constraint is free tooling only. Oracle's Always Free tier includes
20 GB of Object Storage.

## Decision

1. **A nightly `pg_dump`** in PostgreSQL's compressed custom format, run
   inside the postgres container by `deploy/server/backup.sh`. The script
   writes under a temporary name, reads the whole archive back with
   `pg_restore` to prove it is complete, and only then gives it its final
   name, so a failed run never leaves a file that looks like a backup.
2. **Two copies.** The last 7 days stay on the VM, for a quick restore.
   Every backup is also uploaded to a private Object Storage bucket, where
   a lifecycle rule deletes copies after 30 days.
3. **Uploads through a write-only pre-authenticated request.** The VM holds
   a URL that can create objects in the bucket and nothing else: it can't
   read, list or delete them. No Oracle credentials live on the VM, and a
   leaked URL can't expose or destroy the backups.
4. **No extra encryption.** The bucket is private, and Object Storage
   encrypts every object at rest (AES-256) without an option to turn it
   off. A passphrase would protect against little more and could be lost,
   taking every backup with it.
5. **Scheduled by a systemd timer**, not cron: a run missed while the VM
   was off happens when it is back (`Persistent=true`), and every run's
   output lands in the journal. A failed backup or upload exits non-zero,
   so systemd marks the run as failed.
6. **Restores never overwrite.** `restore.sh` restores only into a
   database that doesn't exist yet; replacing the live database means
   dropping it first, deliberately.
7. **A restore drill**, `restore-drill.sh`, restores a backup into a
   scratch database, compares its tables and schema version with the live
   database, and drops it again. A backup counts as working only once it
   has been restored.

## Consequences

**Positive:**

- Losing the VM loses at most a day of data.
- A restore brings back the users and the Data Protection keys too, so
  existing sign-ins keep working.
- The off-VM copies can't be read or deleted from the VM, even by an
  attacker who takes it over.
- The scripts are tested against a real PostgreSQL 16, including a
  truncated archive and a rejected upload.

**Negative:**

- **Up to a day of data can be lost.** Continuous archiving (WAL) would
  shrink that to minutes, at the cost of much more setup.
- **The copies stay with one provider.** Losing the Oracle account loses
  both the VM and the bucket.
- **Failures are only visible in the journal.** Nothing alerts anyone; an
  expired upload URL fails every night until someone looks.
- **The upload URL expires** on the date set when it was created, and has
  to be renewed by hand.
- **The drill is run by hand**, not on a schedule.
- **Backups hold personal data** -- e-mail addresses and password hashes
  -- and the bucket's privacy is what protects it.

## Alternatives Considered

- **Copying the Docker volume's files** -- a byte copy of a running
  database can be inconsistent; `pg_dump` takes a consistent snapshot.
- **`pg_dumpall`** -- also dumps roles, but the only role is the one the
  container creates itself.
- **Pulling backups to a laptop with `scp`** -- no cloud setup, but it
  happens only when someone remembers.
- **The OCI command-line tool with API keys** -- full access to the bucket
  from the VM, which is exactly what a write-only URL avoids.
- **Another provider's free storage** (Backblaze B2, Cloudflare R2) -- a
  copy outside Oracle, but another account and another set of credentials.
- **Encrypting with `gpg` before upload** -- protects against a leaked
  bucket, at the risk of losing the passphrase.
- **cron** -- simpler, but a run missed while the server is off is simply
  skipped, and its output goes nowhere by default.
