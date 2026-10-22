# 41. Azure bootstrap and keyless sign-in from GitHub Actions

## Status

Accepted. Builds on [ADR 0039](./0039-azure-foundation-in-bicep.md) and
[ADR 0040](./0040-services-on-azure-container-apps.md).

## Context

The Azure templates deploy into a resource group that has to exist first,
and the sprint's goal is deploying from GitHub Actions without a stored
key. On AWS the same design was blocked by the account's service control
policy ([ADR 0036](./0036-aws-deploys-from-the-laptop.md)); an Azure
free-trial subscription has no such policy, and its owner can register
trust in an external identity provider.

Microsoft Entra ID supports *workload identity federation*: an identity
gets a federated credential naming an external issuer (GitHub's OpenID
Connect issuer), an audience and an exact subject. A job asks GitHub for a
signed token, Entra ID checks those three values and the signature, and
returns an Azure token for the identity. Nothing secret is stored on
either side.

The subject GitHub puts in a job's token depends on the repository: for
repositories created before 15 July 2026 it is
`repo:<owner>/<repo>:environment:<name>`; newer ones, this one included,
get an immutable form that adds the owner's and the repository's numeric
IDs. A credential whose subject differs by one character refuses every
sign-in.

## Decision

1. **A subscription-level bootstrap template**, `deploy/azure/bootstrap`,
   deployed once from the laptop by the subscription's owner (and again
   after a full teardown). It creates:
   - `rg-finance-tracker`, for the system;
   - `rg-finance-tracker-ci`, holding only the deploy identity, so tearing
     the system down leaves the GitHub trust in place;
   - `id-finance-tracker-deploy`, a user-assigned managed identity, with a
     federated credential for GitHub's issuer, the audience
     `api://AzureADTokenExchange`, and the subject of jobs in this
     repository's `azure` environment.
2. **The subject is read, not guessed.** A manual workflow, *Azure sign-in
   check*, prints the subject of the token its job receives, then signs in
   with `azure/login` and reads the resource group. The printed value is
   the bootstrap's `githubSubject` parameter.
3. **Least privilege on one resource group.** The deploy identity gets
   Contributor on `rg-finance-tracker` (create, change, delete resources)
   and Role Based Access Control Administrator there with a condition: it
   may assign and remove only the Monitoring Metrics Publisher role, the
   one role the templates assign. It has no rights on the subscription or
   on its own resource group.
4. **The `azure` GitHub environment gates the trust.** The federated
   credential matches only jobs that ran in that environment, which
   requires an approval by its reviewer and runs only from `master`. Its
   secrets hold the identity's client ID, the tenant ID and the
   subscription ID; none of them is a credential.

## Consequences

**Positive:**

- No password, key or certificate for Azure exists in GitHub or anywhere
  else; a GitHub token is valid for minutes and only for this subject.
- A leaked workflow log or secret value gives nothing usable.
- Even a compromised deploy job can't grant itself more rights or touch
  anything outside the system's resource group.
- The keyless design that couldn't run on AWS runs here.

**Negative:**

- **A full teardown removes the deploy identity's role assignments** with
  the resource group; the bootstrap has to run again before the next
  deploy. It is idempotent.
- **The subject is tied to the environment name and the repository's
  IDs.** Renaming the environment, or recreating the repository, needs a
  new credential.
- **Creating the Container Apps environment may need more than the
  resource group**: Azure creates its infrastructure resource group in
  the subscription. The first deploy runs from the laptop as the owner;
  GitHub deploys then update what exists.

## Alternatives Considered

- **A service principal with a client secret in GitHub** -- works
  everywhere, but a long-lived secret in a public repository's CI.
- **An app registration with a federated credential** -- equivalent, but
  an Entra ID application object to manage beside the identity; a
  managed identity is an Azure resource, created by the same template.
- **Owner or User Access Administrator for the deploy identity** --
  simpler, but either could grant any role to anyone.
- **Matching branch pushes instead of an environment**
  (`ref:refs/heads/master`) -- no approval step before a job can sign in.
