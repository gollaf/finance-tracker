# 22. CI pipeline and container image publishing

## Status

Accepted

## Context

Until now the only CI was a single GitHub Actions job that restored, built
and tested the solution on pushes and pull requests to `master`. Everything
else was checked by hand, on a laptop:

- **The Dockerfiles.** They copy a hand-picked list of `.csproj` files
  before `dotnet restore` (ADR 0007), so a change can pass `dotnet test`
  and still break `docker build` -- and nothing noticed until the next
  `docker compose up` or `minikube image load`.
- **The Kubernetes manifests** in `deploy/k8s/` (ADR 0019). Kubernetes
  ignores fields it doesn't recognize, so a misspelled field is silently
  dropped rather than rejected.
- **Images existed only where someone built them.** ADR 0020 tags every
  local build `:dev` and noted that "a real pipeline would give every build
  its own tag". A later deployment to a server (planned: an Oracle Cloud
  free VM) needs a tested image it can pull, not one it has to build.

Constraints: free tooling only (`PROJECT_PLAN.md`). The repository is
public, so GitHub-hosted runners and GitHub Container Registry storage for
public packages cost nothing.

## Decision

1. **One workflow, `.github/workflows/ci.yml`**, triggered by pull requests
   to `master`, pushes to `master`, and manually (`workflow_dispatch`). The
   `GITHUB_TOKEN` is read-only by default; a job that needs more declares
   it. Runs are grouped per branch: on a pull request a new push cancels the
   older run; on `master` a started run is never cancelled, so runs happen
   one after another.
2. **`build-and-test`** restores, builds and runs every test suite,
   including the Testcontainers ones (GitHub's Ubuntu runners have Docker).
   NuGet packages are cached with a key derived from every `.csproj`. Test
   results are written as TRX files and published as a check and a run
   summary by `dorny/test-reporter`.
3. **`docker-image`** builds the Api and Worker images (a matrix of two
   jobs) after `build-and-test` passes, from the checked-out folder with the
   same Dockerfiles and build context as a local `docker build`. BuildKit
   layers are cached in the GitHub Actions cache, one scope per image, in
   `mode=max` so the build stage's `dotnet restore` layer is cached too.
4. **Images are published to GitHub Container Registry, from `master`
   only.** A run on `master` logs in to `ghcr.io` with the workflow's own
   `GITHUB_TOKEN` (no stored secret) and pushes
   `ghcr.io/gollaf/finance-tracker-api` and `-worker` with two tags:
   `sha-<7-character commit>`, which never moves, and `latest`, which
   follows the newest `master` build. OCI labels from
   `docker/metadata-action` -- notably `org.opencontainers.image.source` --
   link both packages to this repository. Both packages are public. Pull
   request runs build the same images but push nothing.
5. **`k8s-manifests`** renders `deploy/k8s` with `kustomize build` and
   validates every object with `kubeconform -strict` against the schemas of
   the Kubernetes version the local cluster runs (1.37). Both tools are
   pinned and their downloads checked against the projects' published
   SHA-256 checksums. The gitignored `secrets.env` is replaced by the
   committed `secrets.env.example`, which is all a render needs; nothing is
   applied to any cluster and no credential is involved.
6. **Delivery, not deployment.** The pipeline ends with images in a
   registry. Nothing is deployed automatically yet: there is no target
   environment to deploy to.
7. **`master` is protected by a repository ruleset**: changes only through
   a pull request, the four checks above (`build-and-test`,
   `docker-image (api)`, `docker-image (worker)`, `k8s-manifests`) must
   pass on a branch that is up to date with `master`, and force pushes and
   deletion are blocked. Nobody is on the bypass list. No approvals are
   required -- GitHub doesn't allow approving your own pull request.

## Consequences

**Positive:**

- A broken Dockerfile or an invalid manifest fails the pull request that
  introduced it, not a later local run.
- Every commit on `master` has passed its tests, and its images are in a
  registry under a tag that identifies it exactly. A deployment can name
  `sha-<commit>` and get precisely what CI tested.
- No long-lived credential exists anywhere: publishing uses the per-run
  `GITHUB_TOKEN`, and manifest validation never sees a real secret.
- The README badge and the pull request checks now cover the whole
  build, not just the C#.

**Negative:**

- **The cluster manifests don't use the published images.** `deploy/k8s`
  still references `finance-tracker-*:dev` for the local Minikube workflow
  of ADR 0020. Running the published images in a cluster needs an overlay
  (or similar) that swaps the image reference -- left to the deployment
  work that needs it.
- **Not every `master` commit gets its own image.** While one `master` run
  is in progress only the newest waiting run is kept, so when several pull
  requests are merged in quick succession the ones in between are skipped.
  `latest` is still always the newest commit.
- **`latest` is mutable**, as every moving tag is. Deployments should pin a
  `sha-` tag.
- **Pull request runs of `docker-image` hold a token with
  `packages: write`**, because a job's permissions can't depend on a
  condition. They never log in or push; pull requests from forks get a
  read-only token regardless.
- **Manifest validation checks shape, not meaning.** A wrongly typed or
  misspelled field fails; a selector that matches no Pod, a missing Secret
  key, or a container that crashes on start does not. Those need a real
  API server.
- **Published versions accumulate** in GHCR; nothing deletes old ones.
  Free for public packages, but the list grows.
- **Required check names are the job names.** Renaming a job, or a matrix
  entry, leaves the old name required and pending forever until the
  ruleset is updated.
- **Actions are pinned to major-version tags** (`@v7`), so a new minor or
  patch release of a third-party action is picked up automatically.
- **CI takes longer** than before: images are built only after the tests
  pass, and an out-of-date pull request has to be updated and re-run
  before it can be merged.

## Alternatives Considered

- **Docker Hub** -- needs a separate account and a stored access token, and
  applies pull rate limits; GHCR authenticates with the workflow's own
  token and sits next to the code.
- **Publishing on every run, including pull requests** -- would fill the
  registry with images of unmerged code, and give pull request runs a reason
  to log in to the registry.
- **A separate publish workflow** (`workflow_run` after CI) -- keeps the
  write permission out of pull request runs, but has to rebuild the image or
  hand it over between workflows; more moving parts for a single-maintainer
  repository.
- **Pinning actions to full commit SHAs** -- protects against a moved or
  compromised tag, at the cost of manual updates for every release. A
  reasonable next step together with automated update pull requests.
- **NuGet lock files** with `setup-dotnet`'s built-in cache -- more
  reproducible restores, but a new file in every project that changes with
  every package update. The `.csproj`-hash cache key needs no project
  changes.
- **`GitHubActionsTestLogger`** for test reporting -- adds a package to
  every test project; the TRX logger is built into the test platform.
- **A Kind cluster in CI** that applies the manifests and waits for the
  Pods (ADR 0019 named Kind as the natural fit) -- catches what schema
  validation can't, but is the slowest and most fragile job to run on
  every pull request. Deferred.
- **kubeval** -- the older schema validator, no longer maintained;
  kubeconform is its successor and uses up-to-date schemas.
- **Path filters** (validate manifests only when `deploy/` changes) --
  a workflow skipped by a path filter leaves its required checks pending,
  which blocks the merge.
