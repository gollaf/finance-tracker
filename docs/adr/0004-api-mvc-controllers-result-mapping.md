# 4. API layer: MVC controllers and a fixed Result-to-HTTP mapping

## Status

Accepted

## Context

Every Application handler returns `Result` or `Result<TValue>` instead of
throwing, carrying a stable `ErrorType` (`None`, `Validation`, `NotFound`,
`Conflict`, `Failure`) on failure — see
`src/FinanceTracker.Application/Common/Result.cs` and `Error.cs`. Phase 2
needs two decisions settled before the first controller is written, so
every endpoint follows the same shape instead of each action inventing its
own: how HTTP endpoints are structured, and how a failed `Result` becomes
an HTTP response.

## Decision

Use ASP.NET Core MVC **controllers**, one per aggregate
(`AccountsController`, `TransactionsController`, `CategoriesController`,
`BudgetsController`, `CategorizationRulesController`). Each action builds
a MediatR command/query from the incoming request and calls `ISender.Send`
— no business logic in the controller itself.

`Result` failures are mapped to `IActionResult` through one shared
extension method (e.g. `ResultExtensions.ToActionResult`), not repeated
per action, following this fixed table:

| ErrorType    | HTTP Status               | Body                                            |
|--------------|----------------------------|--------------------------------------------------|
| Validation   | 400 Bad Request            | ProblemDetails; `Error.Message` as `detail`       |
| NotFound     | 404 Not Found              | ProblemDetails                                    |
| Conflict     | 409 Conflict               | ProblemDetails                                    |
| Failure      | 500 Internal Server Error  | ProblemDetails, generic message (no internals)    |

Success status is decided per action rather than folded into the shared
mapping, since only the failure path is uniform across every use case:
`201 Created` with a `Location` header for creates, `200 OK` for queries
and updates, `204 No Content` for deletes.

Any exception that is *not* already a `Result` failure — a genuine bug,
such as a Domain invariant throwing after `ValidationBehavior` should have
already rejected the input, per the comment in
`CreateAccountCommandHandler` — is handled globally via
`UseExceptionHandler`, not per-controller `try`/`catch`, and returns `500`
with a generic ProblemDetails body that does not leak exception details.

## Consequences

**Positive:**

- Every controller action is thin and looks the same: build request →
  `Send` → `result.ToActionResult(...)`. New endpoints in later phases
  follow an established pattern instead of each author choosing their own
  error handling.
- Centralizing the mapping means changing how a given `ErrorType` is
  represented (status code, body shape) is a one-line change, not a
  search-and-replace across every controller.
- Matches ASP.NET Core's built-in `ProblemDetails` (RFC 7807) support,
  which the Swagger/OpenAPI docs (later in Phase 2) pick up automatically.

**Negative:**

- MVC controllers carry more ceremony than Minimal API endpoint groups
  would have (attribute routing, `[ApiController]`, model binding
  conventions) — a deliberate trade against the more novel Minimal API
  style, in favor of the more widely-recognized pattern.
- A generic `ToActionResult` can't fully cover every action's *success*
  shape without a little per-action input (e.g. which `Location` URL to
  use for a `201`) — only the failure path is fully generic.

## Alternatives Considered

- **Minimal API endpoint groups**, mirroring the Application layer's
  per-feature-folder structure (`MapAccountEndpoints()` etc.) — considered
  as the more modern, lower-ceremony option matching the existing
  vertical-slice folders; not chosen, in favor of the more traditional and
  widely-recognized Controllers style.
- **Per-action manual status-code handling, no shared mapping** —
  rejected; would have guaranteed the mapping drifted once more than a
  couple of controllers existed.
- **Throwing custom exceptions from handlers instead of returning
  `Result`, caught by a global filter** — rejected; would reopen a
  decision Phase 1 already made deliberately. The whole point of
  `Result`/`Error` is that a failure is an ordinary return value each
  layer can inspect, not an exception every layer up the stack has to
  interpret.
