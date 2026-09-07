# 6. CSV import parsing lives in the API layer

## Status

Accepted

## Context

`ImportTransactionsFromCsvCommand` (Application, Phase 1) already takes an
`IReadOnlyList<CsvTransactionRow>` -- already-structured rows -- and
`CsvTransactionRow`'s own doc comment explicitly defers "reading and
parsing the actual CSV file" as an I/O concern for whichever layer
eventually accepts the upload. This was flagged as an optional ADR back in
Step 1 of Phase 2's plan, deferred until CSV import was actually being
built (Step 7), because the right answer depends on exactly how the file
arrives over HTTP -- not worth deciding speculatively.

## Decision

1. `TransactionsController.Import` accepts the file as `multipart/form-data`
   (`IFormFile`), not JSON -- HTTP has no other standard way to carry a
   file. `ImportTransactionsRequest` is the one request DTO in the API
   that's a plain class instead of a record, since form binding needs
   settable properties rather than a record's constructor binding.
2. Parsing raw CSV text into `CsvTransactionRow` is done by
   `CsvTransactionRowParser`, a small hand-rolled `internal static` class
   living in the API project (`src/FinanceTracker.Api/Transactions/`) --
   not Infrastructure, and not a new NuGet dependency such as CsvHelper.
   It expects a header line (skipped, not validated), supports
   double-quoted fields to allow an embedded comma, and requires
   `OccurredOn` as `yyyy-MM-dd`.
3. A row that fails to parse (bad syntax) is treated the same way
   `ImportTransactionsFromCsvCommandHandler` already treats a row that
   parses but fails a domain rule: excluded from what's sent to the
   command and reported as one entry in a combined `Errors` list, never
   as a whole-request failure. `ParsedCsvRow.OriginalIndex` lets the
   controller translate the command's own `RowIndex` (relative to the
   filtered list it was actually given) back into the file's real line
   number.
4. If every row fails to parse, the command is never called -- sending it
   an empty `Rows` list would only trigger its own generic "at least one
   row is required" validation failure, burying the real per-row parse
   errors behind a misleading 400.

## Consequences

**Positive:**

- Application stays exactly as Phase 1 left it -- it never has to know
  about `IFormFile`, multipart requests, or raw CSV text, keeping the
  "only ever sees structured rows" boundary from `CsvTransactionRow`'s own
  doc comment intact.
- No new dependency for something this project's needs don't require --
  the format is fixed and simple enough that hand-rolled parsing is a few
  dozen lines, not a real reason to pull in a library.
- The two-layer "one bad row doesn't sink the batch" philosophy (syntax
  errors caught here, domain-rule errors caught in the handler) gives a
  consistent experience regardless of which kind of error a row has.

**Negative:**

- The parser is intentionally minimal -- no configurable delimiter, no
  validation that the header matches the expected column order (it isn't
  even checked), no BOM handling beyond whatever `StreamReader`'s default
  encoding detection gives it. Fine for a portfolio project importing an
  already-known format; a real bank-statement importer would need to be
  more forgiving.
- Skipping the command call entirely when every row fails to parse means
  an unknown or closed `AccountId` goes unreported in that specific
  all-garbage-file edge case -- a minor, accepted gap versus the
  alternative of a misleading generic 400.

## Alternatives Considered

- **A CSV library (e.g. CsvHelper) instead of hand-rolled parsing** --
  rejected for now. The format needed (four fixed columns, optional
  quoting) is simple enough that a library would add a dependency without
  saving meaningful code; worth reconsidering if the format grows
  (multiple bank export formats, configurable column mapping).
- **Parsing in Infrastructure instead of the API project** -- rejected.
  Nothing about turning HTTP-uploaded text into rows touches the database
  or EF Core; it's translating one I/O format (an uploaded file) into
  another (Application's DTO), exactly what the API layer already does
  for every other request.
- **Failing the whole request on the first malformed row** -- rejected.
  Same reasoning `ImportTransactionsFromCsvCommandHandler` already applies
  to domain-rule failures: a bank statement with one bad line shouldn't
  block importing the other two hundred good ones.
