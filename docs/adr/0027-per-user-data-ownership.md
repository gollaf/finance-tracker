# 27. Per-user data ownership

## Status

Accepted. Amends [ADR 0015](./0015-integration-event-contracts.md): event
payloads also carry the owner's id.

## Context

Since ADR 0026 every caller has to sign in, but all signed-in users still
see and change the same accounts, transactions, categories and budgets.
Before the server deployment (ADR 0025) can be made public, each user must
see only their own data.

The data forms two groups. Accounts, transactions and import jobs belong
to an account. Categories, categorization rules and budgets are not tied
to an account at all: a budget sums spending in a category across every
account. Work also happens outside a web request: the Worker processes
imports and asks the AI for a category, with no signed-in user in sight.

## Decision

1. **Every aggregate has an owner.** `Account`, `Transaction`,
   `ImportJob`, `Category`, `CategorizationRule` and `Budget` get an
   `OwnerId` of a new value object `UserId` (a `Guid`, the Identity user's
   key). It is set once by `Create` and never changes; a default
   (all-zero) owner is rejected by the domain.
2. **The owner is passed explicitly, everywhere.** Every command and query
   carries an `OwnerId`, its validator rejects a default one, and every
   repository method that reads data takes the owner and filters on it.
   There is no unscoped read method to call by mistake. Controllers take
   the owner from the signed-in user's `NameIdentifier` claim
   (`User.GetUserId()`); the Worker takes it from the event.
3. **Integration events carry the owner.** `TransactionAdded` and
   `ImportRequested` gain an `OwnerId` (a plain `Guid`, per ADR 0015), so
   the Worker can scope its work without a signed-in user.
4. **Data reached across users is "not found".** Another user's account,
   transaction, import job, category, rule or budget answers 404, exactly
   like an id that doesn't exist -- including when it is only referenced,
   for example a budget on someone else's category. A 403 would confirm
   that the id exists.
5. **Names and lookups are per user.** Category names are unique per
   owner; the AI is offered only the owner's categories; a CSV import
   applies only the owner's rules; a budget counts only the owner's
   spending.
6. **No foreign key from `OwnerId` to `AspNetUsers`.** The column is
   indexed (every lookup filters on it) but not constrained. Users can't
   be deleted yet, so the key would protect nothing today, and it would
   tie every domain table to Identity's table.
7. **Existing data is wiped, not migrated.** The rows written before this
   change have no owner. The server holds only demo data, so the volumes
   are deleted once on deploy instead of assigning old rows to an
   arbitrary user.

## Consequences

**Positive:**

- The data of one user can't leak to another through the API, the
  Worker or the AI prompt, and the tests check this across users at every
  layer: domain, handlers, repositories against PostgreSQL, the HTTP API
  and the Worker pipeline.
- Forgetting the owner is a compile error, not a silent leak: every
  repository read needs it.
- The Worker stays independent of who is signed in.

**Negative:**

- Every command, query, repository method and test grew an extra
  parameter.
- Correctness depends on every query filtering on the owner; a new query
  that forgets it would leak. A global query filter would make the
  default safe but hides the filter, and the Worker has no user for it.
- The one-time wipe deletes all data on the server.
- Messages already queued before the change carry no owner; the wipe
  removes the queues too.

## Alternatives Considered

- **An EF Core global query filter on `OwnerId`** fed by a "current user"
  service -- filters automatically, but invisibly, needs a current user
  that the Worker doesn't have, and is easy to switch off by accident
  with `IgnoreQueryFilters`.
- **Owner only on `Account`**, reaching transactions through their
  account -- fewer columns, but every transaction query would need a join,
  and categories and budgets aren't tied to an account at all.
- **Shared categories with per-user overrides** -- convenient defaults,
  but a second model to keep consistent; per-user categories are simpler.
- **403 Forbidden for another user's data** -- more explicit, but it
  confirms the id exists.
- **Migrating old rows to the first registered user** -- keeps the data,
  but the server holds only demo data.
