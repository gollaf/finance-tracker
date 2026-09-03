# 3. EF Core persistence mapping for strongly-typed IDs and value objects

## Status

Accepted

## Context

Phase 1 fixed the Domain layer's shape deliberately: `AccountId`,
`TransactionId`, `CategoryId`, `BudgetId`, and `CategorizationRuleId` are
each a `readonly record struct` wrapping a `Guid`; `Money` is an immutable
`record` (Amount + Currency); `BudgetPeriod` is a small value object
(Year + Month). Every aggregate root (`Account`, `Transaction`, `Category`,
`Budget`, `CategorizationRule`) has a single private constructor taking all
its fields, no parameterless constructor, and private setters — invariants
are enforced only through factory methods and behavior methods, never
through open properties.

Phase 2 needs a persistence mapping for all of this in PostgreSQL that
doesn't push EF Core's usual requirements (a public parameterless
constructor, public setters, raw `Guid` properties) back into the Domain
layer — doing so would undo the invariant protection Phase 1 built, and
would conflict with `CLAUDE.md`'s constraint that the Domain layer stays
free of persistence concerns.

## Decision

1. **Strongly-typed IDs** map via an EF Core `ValueConverter<TId, Guid>`
   per ID type, stored as a Postgres `uuid` column. Each converter is a
   small, independently testable class rather than an inline
   `.HasConversion(...)` lambda repeated per property.
2. **`Money`** maps via EF Core's `ComplexProperty` (complex types,
   available since EF Core 8) onto two columns on the owning table —
   `Amount numeric(18,2)` and `Currency char(3)` — no separate table, no
   shadow foreign key. Applies to `Transaction.Amount` and
   `Budget.LimitAmount`, both always non-null.
3. **`BudgetPeriod`** gets the same `ComplexProperty` treatment (`Year
   int`, `Month int`).
4. **Entity materialization uses EF Core's constructor binding.** Because
   every aggregate's only constructor takes all its fields by name, EF
   Core binds to it directly via reflection at query time — no
   parameterless constructor or public setter is added to any Domain type
   for persistence's sake. Domain stays exactly as Phase 1 left it.
5. **Enums** (`AccountType`, `TransactionType`) map to Postgres as `text`
   via `HasConversion<string>()`, not as integers, so the schema stays
   readable via `psql`/DBeaver and isn't silently corrupted if the C#
   enum's member order ever changes.

## Consequences

**Positive:**

- Zero changes to any Domain type — the entire mapping lives in
  Infrastructure's `IEntityTypeConfiguration<T>` classes.
- `ComplexProperty` avoids the extra join table `OwnsOne` required before
  EF Core 8, keeping the schema flat and query plans simple.
- String-backed enum columns are self-documenting in the database without
  cross-referencing the C# source.

**Negative:**

- Constructor-binding configuration is less commonly documented than the
  conventional public-setter approach, so getting a new aggregate's
  mapping right the first time takes a little more care (constructor
  parameter names must match property names, case-insensitively).
- `ComplexProperty` currently has rough edges around nullable/collection
  value objects upstream in EF Core. Not a problem for any value object in
  this codebase today (`Money` and `BudgetPeriod` are always non-null),
  but if a future value object needs to be nullable, it may need to fall
  back to `OwnsOne` instead. Revisit if that need arises.
- String enum columns cost a few more bytes per row than integers —
  irrelevant at this project's scale.

## Alternatives Considered

- **Adding a parameterless constructor and public setters to Domain
  entities for EF Core's sake** — rejected. This is exactly the invariant
  leak `docs/adr/0002-transaction-separate-aggregate-no-stored-balance.md`
  was written to avoid elsewhere; constructor binding removes the need for
  it entirely.
- **Inline `.HasConversion(id => id.Value, value => new AccountId(value))`
  per ID property instead of a reusable converter class** — functionally
  equivalent, but a converter class can be unit-tested independently of a
  `DbContext` and reused if the ID pattern is extended later (e.g. a new
  aggregate's ID type).
- **`OwnsOne` for `Money`/`BudgetPeriod` instead of `ComplexProperty`** —
  rejected for now; neither value object needs independent nullability or
  a collection shape, so the newer, lower-overhead `ComplexProperty` is
  the better fit. Revisit if a nullable value object is introduced.
