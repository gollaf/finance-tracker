# 2. Transaction as a separate aggregate root; Account has no stored balance

## Status

Accepted

## Context

Two related modeling questions came up while fixing the domain model
ahead of Phase 1:

1. Should `Transaction` be a child entity owned by the `Account`
   aggregate (loaded and saved as part of `Account`), or its own
   aggregate root referenced by `AccountId`?
2. Should `Account` store a `Balance` field that gets mutated every time
   a `Transaction` is added, updated, or deleted, or should balance be
   computed on demand from the transactions themselves?

These two questions are coupled: the answer to the first constrains the
options for the second.

This project is explicitly modeled after Mint/YNAB-style tools, where
transaction volume per account grows into the thousands over time.
`PROJECT_PLAN.md` also already lists `GetAccountBalance` as a *query*,
not a plain property read, which is itself a hint about where this was
heading.

Phase 1 has no real persistence yet (Infrastructure/EF Core is Phase 2)
and no messaging (RabbitMQ is Phase 5), so any answer also has to work
using only in-memory/mocked repositories in unit tests.

## Decision

`Transaction` is its own aggregate root, referencing `Account` and
`Category` only by ID (`AccountId`, `CategoryId?`) — never as a
navigation property, and never owned/loaded through `Account`.

`Account` does not store a `Balance` field and has no method that mutates
one. Balance is computed on demand by the `GetAccountBalance` query,
which sums the `SignedAmount` (amount with direction applied per
`TransactionType`) of every `Transaction` for that account, via
`ITransactionRepository`. The same approach applies to
`GetSpendingSummary`.

## Consequences

**Positive:**

- `Account` never needs to load an unbounded Transaction collection to
  enforce an invariant or to be saved — aggregates stay small.
- No dual-write / cross-aggregate consistency problem to solve in
  Phase 1: `AddTransaction`, `UpdateTransaction`, and
  `DeleteTransaction` only ever touch the `Transaction` aggregate.
  There's nothing on `Account` that could drift out of sync with the
  Transactions that back it, because nothing on `Account` is derived
  from them.
- Matches `PROJECT_PLAN.md` treating `GetAccountBalance` as a query
  rather than a field read.
- Transactions can be paged, filtered, and queried independently of
  their Account, which `GetTransactions` needs anyway.

**Negative:**

- `GetAccountBalance` and `GetSpendingSummary` do real work (a sum over
  potentially many rows) instead of a cheap field read. Acceptable at
  personal-finance-tracker scale; if this ever becomes a problem, an
  explicit read-model/projection can be introduced later without
  changing the aggregate boundary.
- If domain events are introduced in Phase 5 for async processing, this
  decision will need revisiting — at that point a maintained/cached
  balance updated via events becomes a reasonable option again. Not
  worth building now.

## Alternatives Considered

- **Transaction as a child entity of Account, Account stores Balance,
  mutated via `Account.ApplyTransaction(...)`** — rejected. This is the
  "obvious" first design, but it forces `Account` to load and hold a
  collection of Transactions to stay consistent, which doesn't scale,
  and it introduces a stored value (`Balance`) that has to be kept in
  sync with a separate write path with no transactional or
  event-driven mechanism to do so safely until Phase 2/5 land.
- **Transaction as separate aggregate, but Account still stores a
  maintained Balance updated by the Application layer on every
  transaction write** — rejected for Phase 1. Without a real unit of
  work (EF Core `SaveChanges` wrapping both writes, or a domain event)
  there's no way to guarantee the two stay consistent; deferring this
  until Infrastructure exists avoids building something that can
  silently drift.
