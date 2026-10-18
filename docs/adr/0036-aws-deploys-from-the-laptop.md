# 36. AWS deploys from the laptop

## Status

Accepted. Builds on [ADR 0032](./0032-aws-deployment-with-cdk-fargate-and-rds.md).

## Context

The server is deployed from GitHub Actions
([ADR 0031](./0031-deploying-from-github-actions-over-ssh.md)); the AWS
stacks have been deployed from a laptop, signed in with `aws login`. A
workflow would need AWS credentials of its own.

The safe way for GitHub to get them is OpenID Connect: IAM trusts GitHub
as an identity provider, and each job exchanges a token GitHub signs for
credentials that expire within an hour, so no key is stored anywhere. That
was designed, tested and tried: a stack with GitHub as an identity
provider and a role that only jobs of this repository's deployment
environment could assume, allowed only to switch to the CDK's bootstrap
roles.

The deploy failed on the first resource:

```
not authorized to perform: iam:CreateOpenIDConnectProvider ...
with an explicit deny in a service control policy
```

The account was created through AWS's simplified sign-up. Such accounts
belong to an organization whose service control policies AWS manages;
an explicit deny in a service control policy overrides every permission
inside the account, including administrator rights. The account cannot
register an external identity provider.

## Decision

1. **Deploys stay on the laptop**, with the temporary credentials of
   `aws login` and the commands in the README and the
   [AWS runbook](../aws-operations.md).
2. **No access key as a fallback.** An IAM user's key in a GitHub secret
   would work in this account, but it never expires, and this repository
   and its workflow logs are public.
3. **The account is not upgraded** out of the simplified experience to
   lift the restriction: the sprint stays on the free plan.
4. **The runbook covers the whole lifecycle**: deploying, checking what a
   deploy would change, tearing down part or all of it, confirming nothing
   is left running, and removing the CDK bootstrap at the end of the
   sprint.

## Consequences

**Positive:**

- No credential for AWS exists outside the laptop's short-lived sign-in.
- Nothing to set up or maintain on the GitHub side.

**Negative:**

- **Deploying needs the laptop**, its tools and a fresh sign-in, and
  leaves no record in GitHub of who deployed what.
- **Tearing down depends on remembering to.** Nothing reminds you except
  the budget alert, so the runbook's teardown check matters.
- **The keyless design is not shown running.** In an account whose
  policies allow identity providers it would be about one more stack and
  one workflow.

## Alternatives Considered

- **GitHub Actions with OpenID Connect** -- the preferred design, blocked
  by the account's service control policy.
- **GitHub Actions with an IAM user's access key** -- a permanent
  credential in a public repository's CI.
- **Upgrading the account** -- would leave the free plan's sandbox, and
  AWS's documentation doesn't say whether it lifts this restriction.
- **A new account through the advanced sign-up** -- full control over
  policies, but a new account, its own free-tier terms and redoing the
  bootstrap, for a timeboxed sprint.
