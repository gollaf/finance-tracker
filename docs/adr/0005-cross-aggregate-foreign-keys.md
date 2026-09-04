# 5. Cross-aggregate foreign keys without navigation properties

## Status

Accepted

## Context

Three columns have been deliberately left as "plain converted column, no
FK" while `Category`, `CategorizationRule`, and `Budget` were built out:
`Category.ParentCategoryId` (self-referencing), `CategorizationRule.CategoryId`,
and `Budget.CategoryId`. Each one references another aggregate (or, for
`ParentCategoryId`, another instance of the same aggregate) by ID only —
per `docs/domain-model.md` and ADR 0002's precedent for `Transaction`,
Domain never exposes navigation properties across, or within, aggregate
boundaries. The question deferred each time: should Infrastructure still
add a database-level foreign key constraint on a column like this, even
without a Domain navigation property to hang it off of?

Now that `Transaction` is being built — the aggregate with an
unambiguous, required cross-aggregate reference (`AccountId`) plus an
optional one (`CategoryId`) — this can no longer be deferred. It needs
deciding once, consistently, before Transaction's mapping is written, and
then applied retroactively to the three already-deferred columns rather
than leaving them as stragglers.

## Decision

1. Every ID column that references another aggregate's `Id` (or, for
   `Category.ParentCategoryId`, another instance of the same aggregate)
   gets a real Postgres foreign key constraint, configured via EF Core's
   Fluent API using the generic `HasOne<TPrincipal>().WithMany()`
   overload — the overload that takes no navigation property expression
   on either side. Domain gains zero navigation properties; the
   constraint lives entirely in Infrastructure's
   `IEntityTypeConfiguration<T>` classes.
2. Every one of these foreign keys uses `OnDelete(DeleteBehavior.Restrict)`
   — never `Cascade`, never `SetNull`. Deleting an Account or Category
   that's still referenced by another aggregate must fail loudly at the
   database rather than silently cascading (destroying financial history)
   or silently nulling a reference a rule or budget depends on. No
   delete-Account/delete-Category use case exists in the Application
   layer yet, so this is currently inert, but it's the safer default to
   have in place before one is built, rather than adding it under time
   pressure later.
3. Applies to: `Category.ParentCategoryId → Category.Id`
   (self-referencing), `CategorizationRule.CategoryId → Category.Id`,
   `Budget.CategoryId → Category.Id`, `Transaction.AccountId →
   Account.Id`, `Transaction.CategoryId → Category.Id`.

## Consequences

**Positive:**

- Referential integrity is enforced by Postgres itself, not just by
  Application-layer checks that could be bypassed by a bug or a future
  direct-SQL script.
- Domain stays exactly as-is — `HasOne<T>().WithMany()` with no
  navigation expression is fully supported by EF Core precisely for this
  "foreign key without object graph" case.
- One consistent rule across every cross-aggregate/self-referencing ID
  column, rather than a per-aggregate judgment call.

**Negative:**

- `Restrict` means a future "delete Category" or "delete Account" use
  case must explicitly handle the case where dependents exist (reassign,
  block with a `Result.Failure`, or explicitly cascade at the Application
  layer) — it can no longer rely on the database quietly doing the right
  thing. Treated as a feature of the decision, not a cost to defer.
- Four migrations effectively get revisited (three retrofitted, one built
  in from the start for Transaction) rather than getting this right in
  each one originally — a direct cost of having deferred the decision
  three times. Acceptable for a portfolio project: the deferral was
  itself a deliberate, explained choice to wait for a real cross-aggregate
  example rather than guess.

## Alternatives Considered

- **No foreign key constraints at all, integrity enforced only by the
  Application layer** — rejected. Cheap for Postgres to enforce, and
  catches bugs (or bypassed application code) that a check-before-insert
  in a command handler can't.
- **Cascade delete** — rejected. Silently deleting a Transaction's
  history because its Account was deleted, or silently deleting a Budget
  because its Category was deleted, is exactly the kind of "convenient
  until it destroys real data" behavior a finance app shouldn't have.
- **`SetNull` on delete** — rejected for the same reason as Cascade for
  the non-nullable columns (`CategorizationRule.CategoryId`,
  `Budget.CategoryId`, `Transaction.AccountId` — `SetNull` isn't even
  valid on a required column), and for the nullable ones
  (`Category.ParentCategoryId`, `Transaction.CategoryId`) it would
  silently rewrite data rather than surfacing the conflict.
