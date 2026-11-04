# 42. Azure sprint closed without a deploy

## Status

Accepted. Closes the work begun in ADRs
[0037](./0037-telemetry-with-opentelemetry.md) to
[0041](./0041-azure-bootstrap-and-keyless-github-sign-in.md).

## Context

The Azure sprint was planned around Azure's free account: $200 of credit
for 30 days, with no upgrade to pay-as-you-go. To keep the 30-day clock for
deploying, everything that needed no subscription was built first:

- OpenTelemetry in the Api and the Worker, with the Aspire Dashboard
  locally (ADR 0037), and trace context carried through the outbox
  (ADR 0038). Both are in use whatever the platform.
- The Bicep templates for the foundation (ADR 0039) and for Container Apps
  (ADR 0040), compiled and linted in CI.
- The subscription-level bootstrap and the keyless GitHub sign-in
  (ADR 0041). Its sign-in check printed the subject GitHub issues to this
  repository's `azure` environment, which confirmed the new subject format.

The free-account sign-up was then refused after the card and phone checks,
with no reason given ("You're not eligible for an Azure free account").
Microsoft lists reused phone numbers, cards or e-mail addresses among the
causes; a phone number used on another Microsoft account is the likely one.
The only offer left is pay-as-you-go: no credit, no spending cap, billed by
use.

## Decision

1. **The sprint ends without a deploy.** The account is not opened on
   pay-as-you-go: the project's rule is free tiers only, and pay-as-you-go
   has no cap, only budget alerts.
2. **The templates, the bootstrap and their CI checks stay.** They are the
   reviewable result of the sprint, and they compile and lint on every pull
   request.
3. **The sign-in check workflow is removed**, and the `azure` GitHub
   environment with it: there is no subscription to sign in to, so the
   workflow could only fail. ADR 0041 records what it showed.
4. **What was never built is written down**, so a later deploy starts from
   the right place:
   - the code that reads `Database__UseManagedIdentity` and fetches a
     database token (the templates already set it, and the connection
     string has no password, so deploying today fails at the first
     database connection);
   - the Azure Monitor exporter for `APPLICATIONINSIGHTS_CONNECTION_STRING`;
   - alert rules, a deploy workflow and an Azure runbook.
5. **Phase 8 is closed**: the Oracle server is the permanent demo, the AWS
   copy was deployed and documented, and the Azure sprint is prepared but
   not deployed. `docs/cloud-comparison.md` compares the three.

## Consequences

**Positive:**

- Nothing is billed, and no card is at risk.
- The observability work serves every environment, starting locally.
- The Azure design stays complete enough to review and to deploy later.

**Negative:**

- **The templates are checked only by the compiler and linter**, not by a
  real deployment. Details that only a deploy would confirm stay
  unverified: whether init containers count toward an app's CPU and
  memory, whether the proxy's address falls inside the trusted subnet, and
  whether EF Core can create the database as the Entra administrator.
- **There is no keyless deploy running anywhere**: blocked by a policy on
  AWS (ADR 0036), and without a subscription on Azure.

## Alternatives Considered

- **Pay-as-you-go with short sessions** -- deploy, check, delete the
  resource group the same day. Small sums, but real money with no cap.
- **Asking billing support to review the refusal** -- possible at any
  time; Microsoft doesn't promise to reverse it.
- **Azure for Students** -- requires an academic e-mail address.
- **Someone else's subscription** -- not this project's account to bill.
