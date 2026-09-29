# 21. Worker in Kubernetes: no health probes, explicit shutdown window

## Status

Accepted

## Context

The Worker (ADR 0011) runs in the local cluster as two Deployments of the
same image: `outbox-relay` (one replica) and `worker` (consumers, scaled
freely), per ADR 0018. The Api got liveness and readiness probes backed by
its `/health/live` and `/health/ready` endpoints (ADR 0009). The Worker
has no HTTP server, so it has no such endpoints, and the question is
whether it needs probes at all.

What a probe would be for:

- **Readiness** decides whether a Pod receives traffic from a Service.
  Nothing sends traffic to the Worker; it only connects out, to Postgres
  and RabbitMQ. There is no Service for it, so readiness has nothing to
  control.
- **Liveness** restarts a container that is stuck. The question is which
  Worker failures leave the process running but useless:
  - An unhandled exception in any `BackgroundService` (a consumer, the
    relay) stops the whole .NET host, because
    `HostOptions.BackgroundServiceExceptionBehavior` defaults to
    `StopHost`. The process exits and the Deployment restarts it -- no
    probe needed.
  - Losing RabbitMQ is handled by the client's automatic recovery, and the
    initial connection is retried by `RabbitMqConnectionProvider`.
  - Losing Postgres fails individual messages, which are retried by the
    broker (ADR 0012), and individual relay polls, which are retried on
    the next tick (ADR 0013).
  - What remains is a true hang: a deadlock, or a call that never returns.
    None is known in this code.

A second, related question is shutdown. When a Pod is removed, Kubernetes
sends SIGTERM and, after `terminationGracePeriodSeconds` (default 30),
SIGKILL. The .NET host's own `HostOptions.ShutdownTimeout` is also 30
seconds, so with the defaults Kubernetes may kill the process at the very
moment the host is still stopping its services cleanly.

## Decision

1. **No probes on either Worker Deployment.** A crash already ends the
   process, and Kubernetes restarts any container that exits.
2. **`terminationGracePeriodSeconds: 45`** on both, longer than the host's
   30-second shutdown timeout, so a normal shutdown always completes
   before a SIGKILL: consumers stop and nack their in-flight message back
   to the queue, and the relay finishes recording what it has already
   published.

## Consequences

**Positive:**

- No extra endpoint, port, or dependency in a process that otherwise has
  no reason to serve HTTP.
- Every realistic failure is still recovered from: by the process exiting,
  by client-side retries, or by the broker redelivering.
- Shutdown during a rollout or a scale-down is clean, not a hard kill.

**Negative:**

- **A hung Worker would go unnoticed.** A deadlocked process still looks
  "Running" to Kubernetes and is never restarted. For consumers the
  symptom is a queue that stops draining; for the relay, outbox rows that
  stay unpublished.
- **`kubectl get pods` shows `1/1 Running` as soon as the container
  starts**, even while the Worker is still waiting for RabbitMQ. Only the
  logs tell those states apart.

## Alternatives Considered

- **A heartbeat file plus an `exec` liveness probe**: a background loop
  writes a timestamp every few seconds and the probe fails if it is too
  old. Detects a hung host, but only proves the heartbeat loop is running
  -- not that consumers or the relay are making progress -- and adds code
  whose only purpose is the probe.
- **A small HTTP health endpoint in the Worker** -- would need the ASP.NET
  Core shared framework and a larger base image (ADR 0007 chose
  `dotnet/runtime` for the Worker precisely because it serves no HTTP).
- **A liveness probe checking RabbitMQ or Postgres connectivity** --
  rejected for the same reason as in ADR 0009: a dependency outage would
  restart every Worker for a problem restarting cannot fix.
