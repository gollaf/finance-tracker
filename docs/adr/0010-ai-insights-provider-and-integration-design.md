# 10. AI provider choice and synchronous integration design

## Status

Accepted

## Context

Phase 4 adds the first AI-backed use case, `GetSpendingInsights`
(`PROJECT_PLAN.md`), which turns a month's per-category spending into a
plain-language summary such as "You spent 28% more on dining out this
month than your 3-month average." `PROJECT_PLAN.md`'s tech-stack table
names "Ollama (local) / Groq free tier" as the candidate providers, under
the project-wide "no paid subscriptions" constraint. This phase is
explicitly the *synchronous* version -- the AI call happens inline inside
the request/response cycle of `GetSpendingInsightsQuery`. Phase 5 moves AI
processing off the request path entirely via RabbitMQ; nothing here should
make that later move harder than it has to be.

Several coupled decisions have to be made before the first line of AI
integration code:

1. Which provider actually gets called.
2. Where the seam between Application and "an external AI service" lives,
   and what shape it takes.
3. How much of the response the AI is trusted to produce -- specifically,
   whether it computes the spending numbers themselves or only puts words
   around numbers this codebase already computed.
4. What happens to the request when the AI call is slow, errors, or the
   provider isn't configured at all.

## Decision

### 1. Provider: Groq's free tier, not Ollama, for this phase

Verified current as of this phase (free-tier terms move fast in this
space): Groq's free tier requires no credit card and no paid plan, is
reachable over a plain HTTPS OpenAI-compatible `/openai/v1/chat/completions`
endpoint, and its published free-tier rate limits (tens of requests per
minute, thousands of tokens per minute depending on model) comfortably
cover a single developer poking at `GetSpendingInsights` by hand.

Ollama runs a model locally and would need its own service in
`docker-compose.yml` (a third container alongside `api` and `postgres`),
plus a model pull step before first use -- meaningfully more setup weight
for a "synchronous first version" phase whose actual goal is the
Application/Infrastructure integration pattern, not standing up local
model infrastructure. Groq needs only an API key in User Secrets, the same
shape this project already uses for the Postgres connection string
locally (`FinanceTrackerDbContextFactory`) -- see Consequences for why the
key specifically can't reuse `docker-compose.yml`'s existing
environment-variable pattern the way the Postgres credentials do.

This is a provider choice, not an architecture choice -- see decision 2.
Swapping Groq for Ollama, another free-tier API, or a paid one later only
means writing a new class against the same interface described next; no
Application-layer code changes.

### 2. `IInsightsGenerator` is a port in Application; Infrastructure adapts it

`IInsightsGenerator` is defined in
`FinanceTracker.Application/Transactions/IInsightsGenerator.cs`, next to
`ITransactionRepository` -- same pattern, same reasoning as
`docs/domain-model.md`'s aggregate boundaries and every repository
interface so far: Application declares what it needs, Infrastructure
implements it, and `GetSpendingInsightsQueryHandler` depends only on the
interface. `GroqInsightsGenerator` (Phase 4 Piece 2) is the first, and for
now only, implementation, registered in
`FinanceTracker.Infrastructure/DependencyInjection.cs` exactly like every
repository is.

### 3. The AI only rewords numbers the handler already computed

`GetSpendingInsightsQueryHandler` computes every figure itself --
per-category spending for the requested month, and the average of the
same category across the three preceding calendar months, using the same
"load an Account's Transactions, filter, group" approach
`GetSpendingSummaryQueryHandler` already uses (see ADR 0002's consequence
that this is a computed read, not a stored one). This structured data
(`CategoryTrendDto`) is what gets sent to `IInsightsGenerator` -- the
prompt built in `GroqInsightsGenerator` explicitly instructs the model to
describe the numbers it is given, not invent or recompute any of its own.

This is deliberate for a finance application specifically: an LLM
hallucinating a wrong percentage or dollar figure in a spending summary is
a much worse failure than clumsy phrasing. If the AI call fails entirely,
Consequence/decision 4 below means the numbers are still correct and
still returned -- only the prose degrades.

### 4. AI failure never fails the request

`GetSpendingInsightsQueryHandler` treats a failure from
`IInsightsGenerator.GenerateAsync` (timeout, non-2xx response, missing API
key, malformed response) as non-fatal. `SpendingInsightsDto` carries both
the computed `Trends` and a `Narrative` string, plus
`NarrativeGeneratedByAi: bool`. On an `IInsightsGenerator` failure, the
handler builds a plain, templated sentence directly from `Trends` instead
of the AI's prose, sets the flag to `false`, and still returns
`Result.Success` -- never `Result.Failure`. A category with no spending in
the prior three months (average is zero) reports `PercentChange: null`
rather than dividing by zero or claiming an undefined "infinite percent"
increase.

`GroqInsightsGenerator` (Piece 2) enforces a short, explicit HTTP timeout
-- this call sits inline in a request/response cycle in this phase, unlike
Phase 5's eventual RabbitMQ-driven version, so an unbounded wait would
block the caller for as long as Groq takes to respond or hang.

## Consequences

**Positive:**

- `GetSpendingInsightsQueryHandler` is fully unit-testable today with an
  `NSubstitute` fake `IInsightsGenerator`, before `GroqInsightsGenerator`
  exists at all (Piece 1 ships before Piece 2) -- the same
  interface-first sequencing every repository-backed handler in this
  project already followed in Phase 1/2.
- No possible response from Groq (or any future provider) can make this
  endpoint report a spending figure that didn't come from this project's
  own Transaction data -- the AI's blast radius is limited to wording.
- A Groq outage, a wrong/missing API key, or exceeding the free tier's
  rate limit degrades this one query's prose, not its availability.
  `GetSpendingInsights` never 500s because of an external AI dependency.
- Swapping providers later (Groq to Ollama, Groq to a different free
  tier, or paid once budget allows) touches Infrastructure only.

**Negative:**

- The free-tier rate limits that make Groq usable without payment also
  mean this endpoint could get rate-limited under any real load --
  acceptable for a synchronous, low-traffic Phase 4 query hit by one
  developer; the async version in Phase 5 (queued via RabbitMQ, one
  request at a time from a Worker) is a better fit for real usage volume
  and was already the plan independent of this.
- Unlike the Postgres credentials in `docker-compose.yml` (ADR 0008,
  committed plaintext because the compose network isn't externally
  reachable), a Groq API key is a real secret tied to a personal account
  and free-tier quota -- it cannot be committed even as a "local-only
  default." It's supplied via User Secrets in development, the same
  mechanism already used for the connection string outside Docker, and
  will need an explicit (undocumented-until-Piece-3) environment variable
  path for `docker compose up` to pick it up without hardcoding it in the
  compose file.
- Three months of prior data may not exist yet for a new Account (or this
  project's own development data) -- `Trends` can legitimately be empty.
  The handler and `GroqInsightsGenerator` both need to handle that
  without erroring; the AI call is skipped entirely rather than sent an
  empty prompt (Piece 1's handler tests cover this).

## Alternatives Considered

- **Ollama** -- rejected for this phase specifically, not for the
  project overall. Free, private, and zero rate limits, which makes it
  worth revisiting once real usage volume matters or once Phase 6's
  Kubernetes work is already standing up multiple services anyway --
  neither is true yet in Phase 4.
- **Calling Groq directly from `GetSpendingInsightsQueryHandler`, no
  `IInsightsGenerator` interface** -- rejected; it would make the handler
  untestable without a real HTTP call (or heavy mocking of `HttpClient`
  itself) and would violate the same Application-depends-on-abstractions
  rule every repository interface already enforces here.
- **Letting the AI compute the percentage/comparison itself from raw
  transaction data** -- rejected outright for a finance application; see
  decision 3. Numeric correctness has to be guaranteed by this project's
  own code, not an LLM's arithmetic.
- **Failing `GetSpendingInsights` with a 502/503 when the AI call fails**
  -- rejected; the query's primary value (accurate numbers) doesn't
  depend on the AI being reachable, so failing the whole request over a
  wording failure would throw away the useful, already-correct half of
  the response.
