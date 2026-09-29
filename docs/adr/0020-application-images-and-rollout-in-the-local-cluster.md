# 20. Application images and rollout in the local cluster

## Status

Accepted

## Context

ADR 0019 set up the local Minikube cluster with PostgreSQL and RabbitMQ.
Running this project's own code in it -- the Api now, the Worker next --
raises questions the stateful services didn't: where the cluster gets
images that were never pushed to any registry, how a code change reaches
running Pods, how the migration step from ADR 0017 is run and re-run, and
how the Api's configuration (a connection string containing a password)
is assembled without the password appearing in a manifest.

## Decision

1. **Images are built with Docker Desktop and loaded into Minikube.**
   `docker build` (the same Dockerfiles as compose, ADR 0007), then
   `minikube image load <image>`. Minikube runs its own container runtime
   with its own image store, so an image that only exists in Docker
   Desktop is invisible to the cluster until it is loaded. No registry is
   involved.
2. **A fixed tag, `:dev`, with `imagePullPolicy: IfNotPresent`.** Never
   `:latest`: Kubernetes treats `:latest` as "always pull", and pulling an
   image that exists in no registry fails with `ErrImagePull`. Because the
   tag doesn't change between builds, the Pod template doesn't change
   either, so loading a new image does not by itself replace running Pods;
   `kubectl rollout restart deployment/<name>` does. (A real pipeline
   would give every build its own tag, making each deploy a template
   change and a normal rolling update.)
3. **Migrations run as a Job, `migrate`, that deletes itself 5 minutes
   after finishing** (`ttlSecondsAfterFinished`). A completed Job is never
   re-run and can't be changed, so a permanent one would run once and then
   block every later deploy. With the TTL, each `kubectl apply -k` after
   the previous run has been cleaned up creates a fresh Job, and
   migrations run once per deploy. Re-running with nothing pending is a
   no-op (ADR 0017).
4. **The connection string is composed by Kubernetes, not stored.** The
   password comes from the Secret into a `POSTGRES_PASSWORD` variable, and
   `ConnectionStrings__FinanceTracker` is written as a template that
   references it as `$(POSTGRES_PASSWORD)`; Kubernetes expands it when the
   container starts. The manifest holds the connection string's shape, the
   Secret holds the password, and .NET reads the result like any other
   environment variable.
5. **The Api runs as a Deployment of two replicas behind a ClusterIP
   Service**, rolling updates with `maxSurge: 1, maxUnavailable: 0`, probes
   wired to `/health/live` (startup, liveness) and `/health/ready`
   (readiness, which includes the migrations check), and a 5-second
   `preStop` pause so a Pod stops receiving traffic before it shuts down.
   `ASPNETCORE_ENVIRONMENT=Development`, as in compose, for Scalar and
   exception details on a laptop-only cluster.
6. **Access from the laptop is `kubectl port-forward svc/api 5001:8080`.**
   Port 5001 so it never collides with compose's 5000.

## Consequences

**Positive:**

- No registry, account or network dependency to run the whole system
  locally.
- Deploying is one command, `kubectl apply -k deploy/k8s`, which also
  runs pending migrations; new Api Pods only receive traffic once the
  schema is complete.
- No password appears in any committed file, including the connection
  string.

**Negative:**

- **Two manual steps after a code change**: rebuild and load the image,
  then restart the Deployment. Forgetting the restart leaves the old code
  running with no error anywhere.
- **Loading an image is slow** compared to a compose rebuild: the whole
  image is copied from Docker Desktop into Minikube.
- **The migrate Job's logs are only kept for 5 minutes** after it
  finishes.
- **An apply that changes the Job within those 5 minutes is refused**
  ("field is immutable") -- for example right after editing
  `secrets.env`. The fix is deleting the Job and applying again.
- **A password containing `;` would break the composed connection
  string**, since `;` separates its fields. Fine for local passwords;
  worth remembering.
- **`port-forward` to a Service connects to one Pod**, chosen when the
  tunnel opens -- it is not load-balanced. Requests through it always
  reach the same replica.

## Alternatives Considered

- **`minikube image build`**, building directly inside the cluster -- no
  copy step, but a different build tool (BuildKit inside the node) with
  its own caching and flags; `docker build` is the tool this project
  already uses.
- **Pointing the Docker CLI at Minikube's own daemon** (`minikube
  docker-env`) -- only works when Minikube's runtime is Docker; this
  cluster runs containerd.
- **A local registry** (the Minikube `registry` addon) -- closest to
  production, but more moving parts than a single-developer laptop needs.
- **A permanent Job, deleted by hand before each deploy** -- works, but
  forgetting it makes `kubectl apply` fail on the next change.
- **A Kubernetes init container on the Api Pods that migrates** --
  rejected in ADR 0017: it runs once per Pod, not once per deploy.
- **Storing the full connection string in the Secret** -- simpler, but
  puts non-secret settings (host, port, database) into a gitignored file
  where nobody can review them.
