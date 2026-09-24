# 16. Asynchronous CSV import

## Status

Accepted

## Context

`POST /api/transactions/import` currently imports every row inside the
HTTP request and answers with the result. ADR 0011 moved it to the Worker:
the request should only accept the file, and the import should run in the
background, like AI categorization (ADR 0014).

Categorization was easy to make safe to retry: "already categorized? skip"
makes a second run harmless. An import has no such check. Messages are
delivered at least once (ADR 0012), and today every row saves on its own
(`AddAsync` calls `SaveChangesAsync`): if the Worker crashed after
importing 120 of 200 rows, the redelivered message would import rows
1-120 a second time -- duplicate transactions in someone's finances.

## Decision

### 1. An `ImportJob` aggregate records the request and its outcome

`ImportJob` (Domain) holds the Account, the parsed rows (with their line
numbers in the uploaded file), and a one-way status: `Pending`, then
exactly one of `Completed` (with the imported count and every row error)
or `Failed` (with a reason; nothing imported). A job that is no longer
`Pending` is never processed again.

The rows live **in the job**, not in the message: the message only
carries the job id, as ADR 0015 prescribes. The rows are also what
makes a job re-runnable after an outage. `Rows` and `Errors` are `jsonb`
columns rather than child tables: always read and written together with
their job, never queried on their own. A job holds at most 10,000 rows.

### 2. The whole import is one database transaction

A new Application port, `IUnitOfWork.ExecuteAtomicallyAsync`, wraps
several repository calls in one explicit database transaction
(`BeginTransaction` ... `Commit`). Repositories keep saving individually,
but inside it their saves only become permanent together, and an
exception rolls all of them back. Processing a job runs, in one such
transaction: every row's Transaction and outbox event, and the job's
change to `Completed`. So:

- crash **before** the commit: nothing was saved, the job is still
  `Pending`, and the redelivered message imports it cleanly from row 1;
- crash **after** the commit: the job is already `Completed`, and the
  redelivered message skips it.

Either way, each row is imported exactly once. This is the case ADR 0013
anticipated -- one operation changing several aggregates atomically -- and
it is solved without the full unit-of-work refactor ADR 0013 deferred:
existing repositories and handlers don't change at all.

### 3. The HTTP contract becomes 202 Accepted + polling

- `POST /api/transactions/import` still parses the CSV in the Api (ADR
  0006) and still reports rows that fail to parse. It creates a `Pending`
  job, enqueues an `ImportRequested` event through the outbox (same
  commit), and returns `202 Accepted` with the job id and a `Location`
  header.
- `GET /api/imports/{id}` returns the job's status, imported count, and
  row errors.
- A file where no row parses at all still gets an immediate answer with
  the parse errors -- there is nothing to process.

## Consequences

**Positive:**

- An upload returns as soon as the file is parsed, however many rows it
  has.
- A crash or restart at any point never leaves a half-imported file,
  and never imports a row twice.
- The import job is a durable record of what was imported, when, and
  which lines were rejected.

**Negative:**

- **Breaking API change**: clients get a job id and must poll for the
  result, instead of getting it in the response.
- A large import holds one database transaction open for its whole
  duration. Fine for a personal CSV; very large files would call for
  processing in committed batches, with progress stored in the job.
- A job's raw rows stay in the database after processing. Harmless at
  this scale; a cleanup of old completed jobs would be the fix.
- After a rollback, EF Core's change tracker still believes the
  rolled-back entities were saved. That's safe only because the DI scope
  (one per message) ends with the exception; code that caught the
  exception and carried on with the same DbContext would be working with
  a lie.

## Alternatives Considered

- **Rows in the message payload** -- rejected; large messages through the
  outbox and the broker, against ADR 0015's ids-only rule, and nothing
  left to re-run once the message is consumed.
- **Per-row saves with progress stored in the job** ("rows 1-120
  done"), resuming after a crash -- works, but every row then needs its
  own "row + progress" atomic save, which is the same problem one level
  down, repeated 200 times.
- **The full unit-of-work refactor** (repositories stop saving;
  handlers call `SaveChangesAsync`) -- still deferred: the explicit
  transaction gives the same all-or-nothing guarantee here without
  touching any other code.
- **Keeping the import synchronous** -- a fair choice for small files
  (and discussed: with AI categorization already asynchronous, a
  synchronous import is fast), rejected in favor of learning the
  202-and-poll pattern and idempotent long-running jobs.
