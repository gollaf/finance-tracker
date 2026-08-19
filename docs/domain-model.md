# Domain Model — Ubiquitous Language

This document fixes the vocabulary and aggregate boundaries for the
Personal Finance Tracking bounded context before any Phase 1 code is
written. It is the reference every Domain/Application test in Phase 1
should be written against. Significant boundary decisions referenced here
are recorded separately in `docs/adr/`.

## Bounded Context

**Personal Finance Tracking** — a single bounded context for this project.
Accounts, Transactions, Categories, Budgets, and categorization rules all
share one ubiquitous language and one model; there's no need to split
contexts at this scale.

## Glossary

| Term | Meaning |
|---|---|
| Account | A place money is tracked — a checking account, credit card, cash wallet, etc. Does not store a balance itself (see "Balance" below). |
| Transaction | A single dated movement of money, in or out of one Account. The core unit of the system. |
| Category | A label for what a Transaction is for (e.g. "Groceries", "Rent"). Can have a parent Category for hierarchy. |
| Budget | A spending limit set for one Category in one calendar month. |
| Categorization Rule | A pattern matched against a Transaction's description to suggest a Category automatically. Rule-based only in Phase 1 — AI-assisted categorization is Phase 4. |
| Balance | Not a stored value. The sum of a given Account's Transactions' signed amounts, computed on demand by the `GetAccountBalance` query. |
| Signed Amount | A Transaction's Amount with direction applied: positive for Income, negative for Expense. Derived, never stored. |

## Value Objects

**`Money`**
Amount (`decimal`) + Currency (`string`, ISO 4217, 3 uppercase letters — e.g.
`"USD"`). Immutable. Supports `+`/`-` between two `Money` of the *same*
currency only — mismatched currencies throw. No FX conversion in this
project; multi-currency conversion is explicitly out of scope.

**Strongly-typed IDs**
`AccountId`, `TransactionId`, `CategoryId`, `BudgetId` — each a
`readonly record struct` wrapping a `Guid`, with a static `New()` factory.
Used everywhere instead of raw `Guid`, including for cross-aggregate
references, so a `CategoryId` can never accidentally be passed where an
`AccountId` is expected.

**`BudgetPeriod`**
Year (`int`) + Month (`int`, 1–12). Represents "this calendar month's
budget." Equality by value; invariant: Month must be in `[1, 12]`.

## Aggregates

Every cross-aggregate reference below is by ID only — no aggregate holds
a navigation property to another aggregate's entity. This keeps aggregates
small and independently loadable/testable, which matters here because
Transaction volume is expected to grow large (this is a Mint/YNAB-style
tracker, not a toy).

### Account (Aggregate Root)

- `Id: AccountId`
- `Name: string` — required, non-empty, max 100 chars
- `Type: AccountType` enum — `Checking | Savings | Credit | Cash | Investment`
- `Currency: string` — ISO 4217, set at creation, immutable
- `IsClosed: bool` — accounts are closed, never deleted, to preserve
  Transaction history integrity

**Deliberately does not store a Balance.** See "Balance" in the glossary
and `docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md`
for why.

Behavior: `Create(name, type, currency)` factory; `Rename(name)`; `Close()`
(throws if already closed). No invariant currently forbids a negative
balance on any account type — this is a tracker, not a bank, and Credit
accounts are expected to run negative by nature.

### Transaction (Aggregate Root — separate from Account)

- `Id: TransactionId`
- `AccountId: AccountId` — reference only
- `CategoryId: CategoryId?` — nullable; uncategorized until
  `CategorizeTransaction` runs
- `Amount: Money` — always **non-negative**; direction comes from `Type`,
  never from the sign of `Amount`
- `Type: TransactionType` enum — `Income | Expense` (no `Transfer` in
  Phase 1 — see "Out of scope")
- `Description: string` — required, non-empty, max 500 chars
- `OccurredOn: DateOnly` — invariant: not in the future
- `CreatedAt: DateTimeOffset` — audit timestamp, set once at creation

Derived: `SignedAmount` — `+Amount` if `Type == Income`,
`-Amount` if `Type == Expense`. Used only by the `GetAccountBalance` and
`GetSpendingSummary` queries; never stored.

Behavior: `Create(accountId, amount, type, description, occurredOn,
categoryId?)` factory; `Recategorize(categoryId?)`;
`UpdateAmount(Money)`; `UpdateDescription(string)`;
`UpdateOccurredOn(DateOnly)`. Invariants (amount > 0, description
non-empty, occurredOn not in the future) are enforced both at creation and
on every update.

### Category (Aggregate Root)

- `Id: CategoryId`
- `Name: string` — required, non-empty
- `ParentCategoryId: CategoryId?` — optional, for hierarchy (e.g. "Dining
  Out" under "Food")

Behavior: `Create(name, parentCategoryId?)`; `Rename(name)`;
`Reparent(parentCategoryId?)`. Domain-level invariant: a Category cannot
be its own parent. Cross-instance concerns — name uniqueness, deeper
cycle prevention across more than one hop — require querying other
Categories and so are enforced in the Application layer, not here.

### Budget (Aggregate Root)

- `Id: BudgetId`
- `CategoryId: CategoryId` — reference only
- `Period: BudgetPeriod`
- `LimitAmount: Money` — invariant: must be > 0

Behavior: `Create(categoryId, period, limitAmount)`; `UpdateLimit(Money)`.
"One budget per Category per Period" is a cross-instance uniqueness rule,
enforced in the Application layer via the repository, not a Domain
invariant.

### Categorization Rule (Aggregate Root)

- `Id`: (reuse a dedicated `CategorizationRuleId`, same pattern as the
  others)
- `Pattern: string` — case-insensitive substring matched against a
  Transaction's Description
- `CategoryId: CategoryId` — reference only
- `Priority: int` — lower value = matched first

Paired with a stateless **domain service**, `TransactionCategorizer`:
given a description and an ordered set of rules, returns the first
matching `CategoryId`, or `null` if none match. This is a domain service
rather than a method on any single aggregate because it operates across
many `CategorizationRule` instances at once. It is the entire Phase 1
implementation of `CategorizeTransaction` — the AI fallback for
unmatched transactions is Phase 4.

## How cross-aggregate consistency works in Phase 1

Because Transaction and Account are separate aggregates and there is no
real persistence or messaging yet (Infrastructure is Phase 2, RabbitMQ is
Phase 5), Phase 1 has no domain events and no two-aggregate transaction to
keep consistent — deliberately. `AddTransaction` only creates and saves a
Transaction; it does not touch Account at all, because Account has
nothing derived from Transactions to keep in sync. Balance and spending
summaries are computed at query time instead. Revisit this once
Infrastructure/EF Core lands in Phase 2, and again if domain events become
useful for Phase 5's async processing.

## Explicitly out of scope for Phase 1

- **AI-assisted categorization** (Phase 4) — `CategorizationRule` /
  `TransactionCategorizer` above is the rule-based foundation only.
- **Natural-language spending insights** (Phase 4) — if `GetSpendingInsights`
  is attempted in Phase 1 at all, it's the statistical computation only,
  no generated wording.
- **Account-to-account transfers** — not in `PROJECT_PLAN.md`'s command
  list; would need a `Transfer` transaction type or a dedicated aggregate,
  neither exists yet.
- **Multi-currency conversion** — `Money` refuses to combine different
  currencies; there is no FX rate concept.
- **Recurring/scheduled transactions** — not in scope for this phase.
- **Domain Events** — none yet; see "How cross-aggregate consistency
  works" above.
