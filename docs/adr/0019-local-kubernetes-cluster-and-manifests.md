# 19. Local Kubernetes cluster and manifest layout

## Status

Accepted

## Context

docker compose (ADR 0008) runs the whole system on one machine with one
command, and stays the everyday way to run it locally. Running the same
system on Kubernetes needs a handful of decisions before the first
manifest is written: which local cluster, how the YAML is organized and
applied, how secrets reach containers without being committed, and how
the two stateful services -- PostgreSQL and RabbitMQ -- run inside the
cluster. Two application-side prerequisites are already in place: the
`migrate` command and schema-aware readiness (ADR 0017), and the Worker's
relay/consumer roles (ADR 0018).

The constraint from `PROJECT_PLAN.md` still applies: free, local tooling
only.

## Decision

1. **Minikube, with the Docker driver.** A one-node cluster inside a
   single Docker container, started with `minikube start`. Chosen over
   Kind for its built-in addons (dashboard, ingress, metrics-server) and
   `minikube image` commands, which make it the friendlier environment to
   learn on; Kind remains the natural choice for running the same
   manifests in CI later. Chosen over Docker Desktop's built-in
   Kubernetes because nothing here then depends on one vendor's desktop
   product.
2. **Plain manifests, combined with Kustomize.** Every object is written
   as ordinary Kubernetes YAML in `deploy/k8s/`, one file per component.
   `deploy/k8s/kustomization.yaml` lists them and sets the namespace for
   all of them, and the whole system is applied with `kubectl apply -k
   deploy/k8s` (Kustomize is built into kubectl). No Helm: its templating
   language would sit between the reader and the objects this project is
   meant to teach, and there is only one application to package.
   `deploy/` rather than a top-level `k8s/` leaves room for other
   deployment targets.
3. **One namespace, `finance-tracker`**, so the whole system can be
   listed, or deleted, as one unit.
4. **Secrets are generated from a gitignored file.** `deploy/k8s/secrets.env`
   (`KEY=value` lines; `secrets.env.example` is the committed template)
   becomes the Secret `finance-tracker-secrets` through Kustomize's
   `secretGenerator`. Kustomize appends a content hash to the Secret's
   name and rewrites every reference to it, so changing a value rolls the
   Pods that use it automatically. Containers read values through
   `secretKeyRef` environment variables, which .NET configuration picks
   up with no code change.
5. **PostgreSQL and RabbitMQ run in-cluster as hand-written StatefulSets**,
   one replica each, with the same official images as docker compose and
   a PersistentVolumeClaim for their data. Each gets a headless Service
   with the same name compose uses (`postgres`, `rabbitmq`), so connection
   settings keep the same host names. Probes mirror the compose
   healthchecks for readiness; liveness is deliberately more lenient,
   because restarting a database is disruptive.

## Consequences

**Positive:**

- The objects in the repository are exactly the objects in the cluster,
  readable without learning a templating language.
- One command deploys or updates everything; applying it again changes
  nothing.
- No credential is ever committed; rotating one is edit, apply, done.
- The databases survive Pod restarts and rescheduling, like compose's
  named volumes survive `docker compose down`.

**Negative:**

- **Secrets are not encrypted.** A Kubernetes Secret is base64-encoded,
  and stored unencrypted in the cluster by default. Its value is keeping
  credentials out of git and out of manifests, not protecting them from
  anyone who can read the cluster. A real deployment would add encryption
  at rest, or an external secret store.
- **Single-instance databases.** No replication or failover: if the
  Postgres Pod is down, the system is down, exactly as with compose.
  Production-grade in-cluster databases usually mean an operator
  (CloudNativePG, the RabbitMQ Cluster Operator) or a managed service.
- **`POSTGRES_PASSWORD` only applies when the data volume is empty**, a
  behavior of the official image. Changing it in `secrets.env` later does
  not change an existing database's password; that needs `ALTER USER`, or
  deleting the volume.
- Minikube has its own image store, separate from Docker Desktop's:
  application images have to be built into it (or loaded) before Pods can
  use them.

## Alternatives Considered

- **Kind** -- lighter and CI-friendly; no addons or dashboard. A good fit
  for running these same manifests in CI.
- **Docker Desktop's built-in Kubernetes** -- the least setup, but ties the
  environment to Docker Desktop's own Kubernetes integration.
- **Helm** -- the standard for distributing applications to other people;
  more machinery than one application in one environment needs.
- **Bitnami Helm charts for PostgreSQL and RabbitMQ** -- rejected: their
  images moved behind a subscription in 2025, and a chart would hide the
  StatefulSet, PVC and Service this project wants to learn.
- **Operators (CloudNativePG, RabbitMQ Cluster Operator)** -- the right
  direction for production, but they replace exactly the objects being
  learned here with custom resources.
- **Keeping PostgreSQL and RabbitMQ in docker compose and only the Api and
  Worker in the cluster** -- rejected: reaching the host from inside
  Minikube is fiddly and platform-specific, and it leaves half the system
  outside what is being learned.
- **Secret manifests with values committed** (even base64-encoded) --
  rejected; base64 is an encoding, not protection.
