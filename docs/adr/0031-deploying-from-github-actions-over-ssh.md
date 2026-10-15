# 31. Deploying from GitHub Actions over SSH

## Status

Accepted. Amends [ADR 0022](./0022-ci-pipeline-and-image-publishing.md):
master builds are now deployed, not only published.

## Context

CI publishes an image tag for every master commit (ADR 0022). Putting one
on the server (ADR 0025) has been a manual sequence: back up, edit
`IMAGE_TAG` in `.env`, pull, restart, check. Done by hand it is easy to
skip the backup, mistype the tag, or leave a broken version running.

GitHub's runners can reach the server only over SSH. Whatever credential
the workflow holds lives in GitHub, so it should be able to do as little
as possible if it ever leaks.

## Decision

1. **A `Deploy` workflow chained to CI** (`workflow_run`). It starts when
   CI completes and deploys only when that run succeeded and came from a
   push to master -- pull request runs publish no images.
2. **Approval on a GitHub environment.** The job uses the `production`
   environment, which requires a reviewer's approval. Its secrets -- the
   SSH key, the pinned host key and the server's address -- are released
   to the job only after approval.
3. **A key that can do one thing.** The workflow's SSH key is installed
   on the server with `restrict,command="... deploy.sh"`: whatever the
   client asks for, `deploy.sh` runs, with the request in
   `SSH_ORIGINAL_COMMAND`; forwarding and terminals are off. The script
   accepts only a tag of the exact form CI publishes (`sha-` and seven
   hex digits). A leaked key can deploy a published build and nothing
   else.
4. **The server's host key is pinned** in a secret and checked
   (`StrictHostKeyChecking=yes`), so the runner refuses a machine that
   only claims to be the server.
5. **`deploy.sh` does the whole sequence**: back up the database (and stop
   if that fails), pull the new images while the old version keeps
   running, switch `IMAGE_TAG`, restart -- migrations run first -- and
   wait up to 90 seconds for `/health/ready` and a running Worker. If the
   new version doesn't get there, it switches back to the previous tag and
   fails the job.
6. **Images only.** A deploy changes the image tag, never `compose.yaml`,
   the `Caddyfile` or the scripts; changes to those are copied to the
   server by hand, because they may need new `.env` values.
7. **One deploy at a time**, on both ends: a concurrency group in the
   workflow and a file lock in `deploy.sh`.

## Consequences

**Positive:**

- A merge reaches the server with one click, always with a fresh backup
  and a readiness check.
- A version that doesn't start is rolled back automatically.
- Every deploy is recorded in GitHub, with who approved it and the
  server's output.
- Redeploying an earlier version is re-running its old Deploy run.

**Negative:**

- **The rollback doesn't undo migrations.** If a new version's
  migration ran but the version then failed, the previous version runs
  against the newer schema. That works while migrations only add things;
  otherwise the backup taken by the deploy is the way back.
- **A short interruption.** The containers are replaced, not run side by
  side, so the API is unavailable for a few seconds.
- **SSH stays open to the internet**, as GitHub's runners have no fixed
  addresses; only key sign-in is allowed.
- **Server files drift** from the repository unless copied by hand when
  they change.
- **Approval is a click by the same person who merged.** It guards
  against surprise, not against a second opinion.

## Alternatives Considered

- **A plain SSH key with full shell access** -- the usual tutorial setup,
  but a leaked key would own the server.
- **A self-hosted runner on the VM** -- no inbound SSH, but it runs
  workflow code on the server and uses memory the 1-core VM needs.
- **Pull-based updates on the server** (Watchtower, or a timer polling
  `latest`) -- no credentials in GitHub, but no approval, no backup step
  and no record of who deployed what.
- **A ready-made SSH action from the marketplace** -- convenient, but a
  third-party action would handle the private key; the runner's own
  `ssh` does the same in four lines.
- **Deploying without approval** -- faster, but every merge would go live
  unseen.
