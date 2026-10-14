# 25. Single-server deployment with docker compose

## Status

Accepted

## Context

The always-on demo runs on an Oracle Cloud Always Free virtual machine:
Ampere A1 (Arm), 1 core and 6 GB of memory, Ubuntu 24.04, in the
Frankfurt region, with Docker Engine installed. CI publishes the Api and
Worker images for `amd64` and `arm64` (ADRs 0022 and 0024). What remains is
how the system runs on that machine.

The local options already in the repository don't fit as they are:

- **The root `docker-compose.yml`** builds the images from source, runs in
  `Development`, publishes every port on all network interfaces and
  hardcodes local passwords (ADR 0008).
- **`deploy/k8s`** targets Minikube with `:dev` images loaded by hand
  (ADR 0020). Running it on the server would first need a Kubernetes
  distribution there, about half a gigabyte of extra memory on a 6 GB
  machine, and an overlay for registry images.

The Api has no authentication yet, so until it does, nothing it serves may
be reachable from the internet.

## Decision

1. **A separate compose file for servers, `deploy/server/compose.yaml`.**
   It runs PostgreSQL, RabbitMQ, a one-shot `migrate` service, the Api and
   the Worker. The server needs only this file and a `.env` next to it;
   no repository checkout, SDK or build.
2. **Images come from `ghcr.io`, pinned by `IMAGE_TAG=sha-<commit>`**, a
   required value. `latest` is never used: it moves, so the running version
   could not be told from the file, and two pulls of the same file could run
   different code.
3. **`Production` environment, with the API docs on** (`ApiDocs__Enabled`,
   ADR 0023): no exception details in responses, interactive docs for
   reviewers.
4. **Migrations run once per deployment, as the `migrate` service** -- the
   Api image with the `migrate` command (ADR 0017). The Api and Worker wait
   for it to exit successfully (`service_completed_successfully`), and the
   Api runs with `Database__ApplyMigrationsOnStartup=false`.
5. **One Worker process with both roles**, so exactly one outbox relay runs
   (ADR 0018).
6. **Nothing is reachable from outside the server.** PostgreSQL publishes
   no port at all. The Api (port 5000) and the RabbitMQ management UI (port
   15672) are published on `127.0.0.1` only. Docker's published ports are
   handled by Docker's own firewall rules, ahead of the server's normal
   inbound rules, so binding to the loopback address -- not a firewall rule
   -- is what keeps them private. Access is through an SSH tunnel.
7. **Secrets live only in the server's `.env`**, readable only by its owner
   (`chmod 600`). `env.example` in the repository is the template. Passwords
   are generated as hexadecimal strings, because they are inserted into a
   connection string, where a `;` would end the password early. Every
   required value uses `${VAR:?message}`, so compose refuses to start when
   one is missing or empty.
8. **`restart: unless-stopped`** on every long-running service, so the
   system comes back by itself after a reboot or a crash; `migrate` never
   restarts.

## Consequences

**Positive:**

- One file describes exactly what runs on the server, and `IMAGE_TAG` says
  which commit it is.
- Deploying a new version is: change `IMAGE_TAG`, `docker compose pull`,
  `docker compose up -d`. Migrations run first, every time.
- The unauthenticated Api can't be reached from the internet even if a
  cloud or server firewall rule is changed by mistake.
- The server holds no source code and builds nothing.

**Negative:**

- **Deployment is manual** until a deploy job exists: copy files with
  `scp`, edit `.env`, run compose over SSH.
- **Two compose files to keep in step.** A new setting or service added to
  the root `docker-compose.yml` must be added here too.
- **Single machine, single copy of everything.** If the VM is down, the
  demo is down; there is no replication. Backups are a separate concern.
- **Secrets are plain text on the server's disk**, protected by file
  permissions only. Anyone with `sudo` or `docker` access on the VM can
  read them -- which, on this server, is only the owner.
- **The first-start rule still applies**: changing `POSTGRES_PASSWORD` or
  `RABBITMQ_PASSWORD` in `.env` later does not change an existing
  database's or broker's password (ADR 0019).
- **Oracle may reclaim an idle Always Free VM** (CPU, network and memory
  below 20% over 7 days). The system's own load may stay under that line;
  if it does, resizing the VM or changing the account type are the options.

## Alternatives Considered

- **k3s with an overlay of `deploy/k8s`** -- reuses the Kubernetes
  manifests and shows Kubernetes on a real server, but adds a cluster to
  operate and its memory on a 6 GB machine, for one copy of each service.
- **The root `docker-compose.yml` with an override file** -- less
  duplication, but the override would have to undo most of it (builds,
  ports, environment, passwords), and a mistake would expose ports.
- **Building the images on the server** -- needs the repository and the
  .NET SDK there, takes the single core for minutes, and runs code CI never
  tested.
- **The `latest` tag** -- simpler to update, but the running version is no
  longer visible, and a pull can change the system unannounced.
- **Publishing the Api on all interfaces and relying on firewall rules** --
  Docker's published ports bypass the host's inbound rules, so this would
  depend entirely on the cloud firewall.
