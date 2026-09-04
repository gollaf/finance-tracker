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
4. **Entity materialization uses EF Core's constructor binding** for
   every constructor parameter that is a plain scalar or a converted
   value (strongly-typed IDs, enums) — EF Core binds those directly via
   reflection at query time. A parameter mapped as a `ComplexProperty`
   cannot be bound this way; see "Amendment — constructor binding and
   complex-type parameters" below for the small, targeted exception this
   requires.
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
- A constructor parameter mapped as a `ComplexProperty` can never be
  bound by EF Core's constructor binding — the same restriction applies
  to the older `OwnsOne`, so no mapping choice avoids it. See the
  amendment below for the pattern this forces on affected aggregates.
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

## Amendment — constructor binding and complex-type parameters (Step 3, Budget piece)

Building `Budget`'s persistence mapping surfaced a real gap in decision 4
above. `Budget`'s only constructor took `period` (`BudgetPeriod`) and
`limitAmount` (`Money`) as parameters — both mapped via `ComplexProperty`.
EF Core refused to use that constructor at all:

```
No suitable constructor was found for the type 'Budget'. The following
constructors had parameters that could not be bound to properties of the
type:
    Cannot bind 'period', 'limitAmount' in 'Budget(BudgetId id,
CategoryId categoryId, BudgetPeriod period, Money limitAmount)'
Note that only mapped properties can be bound to constructor parameters.
Navigations to related entities, including references to owned types,
cannot be bound.
```

This is a hard EF Core rule, not a quirk of `ComplexProperty`: a
constructor parameter whose type is mapped as a complex type or an owned
type (`OwnsOne`) can never be constructor-bound. If even one parameter of
a constructor is unbindable this way, EF Core discards that whole
constructor as a materialization candidate — it does not fall back to
binding the other parameters and setting the rest via properties. So
switching `Money`/`BudgetPeriod` from `ComplexProperty` to `OwnsOne`
would not have avoided this; the same restriction is named explicitly in
the error text ("references to owned types ... cannot be bound").

**Resolution applied to `Budget`:**

1. `Period` gained a `private set` (it was previously get-only) — the
   same idiom `LimitAmount` already used for `UpdateLimit`.
2. A second, private, EF-Core-only constructor was added —
   `Budget(BudgetId id, CategoryId categoryId)` — taking only the two
   scalar/converted parameters. EF Core selects this constructor (the
   four-parameter one is disqualified by rule 4 above), binds `Id` and
   `CategoryId` through it, then sets `Period` and `LimitAmount`
   afterward via their private setters. Domain code itself never calls
   this constructor — `Budget.Create()` still uses the original
   four-parameter one exclusively.

This is a small, intentional concession to EF Core, not a reopening of
the "no persistence-only members" principle: the added constructor and
setter are both `private`, unreachable from outside `Budget`, and don't
loosen any invariant `Budget.Create()`/`UpdateLimit()` already enforced.

**Consequence for later aggregates:** `Transaction.Amount` is also
`Money`, mapped the same way, so `Transaction`'s constructor will need
the identical two-constructor treatment when its persistence mapping is
built. This is now expected, not a surprise to debug again from scratch.

Decision 4 above is revised accordingly: constructor binding applies only
to scalar/converted constructor parameters; any aggregate with a
complex-type constructor parameter needs a second, narrower, EF-Core-only
constructor covering just the bindable parameters, plus private setters
on the complex-typed properties themselves.
