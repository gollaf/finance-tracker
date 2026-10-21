# 39. Azure foundation in Bicep: network, database, monitoring, identity

## Status

Accepted.

## Context

The Azure sprint runs the system on Azure Container Apps, with telemetry in
Application Insights and no passwords: the containers sign in to the
database and to Application Insights as a managed identity. The account is
a free trial: $200 of credit for 30 days, after which the subscription is
disabled; it is not upgraded. The 12-month free amounts include PostgreSQL
Flexible Server on Burstable B1ms with 32 GB of storage.

This decision covers what the containers need before they exist: the
network, the database, telemetry storage, and the identity. The Container
Apps environment and the apps follow in a decision of their own.

Azure Resource Manager (ARM) is the API every Azure resource is created
through. It accepts JSON templates that describe resources, and works out
the order to create them in. Bicep is Microsoft's language for those
templates: shorter than JSON, type-checked against every resource type's
schema, and compiled to ARM JSON.

## Decision

1. **Bicep in `deploy/azure`**: `main.bicep` and one module per concern
   (`identity`, `network`, `database`, `monitoring`). It is deployed into
   one resource group, created outside the template; deleting that group
   deletes everything. Every resource is tagged `project=finance-tracker`.
2. **CI compiles and lints the templates** (`bicep` job, pinned Bicep CLI),
   with the lint rules in `deploy/azure/bicepconfig.json`; offline, with no
   Azure account.
3. **A user-assigned managed identity**, `id-finance-tracker-apps`, for the
   Api and the Worker. A user-assigned identity is a resource of its own,
   so the database and Application Insights can be told to trust it before
   any container runs.
4. **A virtual network**, `10.40.0.0/16`, with two delegated subnets:
   `snet-apps` (`/24`) for the Container Apps environment and
   `snet-database` (`/28`, the smallest allowed) for PostgreSQL. The
   subnets are declared inside the network resource. The range avoids
   those Container Apps reserves.
5. **PostgreSQL 16 on Flexible Server, Burstable B1ms, private access
   only.** 32 GB of storage without auto-grow, 7 days of backups, no high
   availability, no geo-redundant backups. The server gets an address in
   `snet-database` and no public endpoint; its name is published in a
   private DNS zone, `finance-tracker.private.postgres.database.azure.com`,
   linked to the network. The server name ends in a hash of the resource
   group's ID, since it must be unique across Azure.
6. **Microsoft Entra authentication only.** Password authentication is
   disabled and no administrator login exists. The apps identity is the
   server's Entra administrator; its name is its PostgreSQL role. The
   application database is not created by the template: EF Core's
   migrations create it, so its owner is the identity that also creates
   the tables.
7. **Log Analytics and workspace-based Application Insights.** 30 days of
   retention and a 1 GB daily ingestion cap on the workspace. Application
   Insights accepts telemetry only with an Entra token (`DisableLocalAuth`);
   the apps identity has the Monitoring Metrics Publisher role on it.
8. **Outputs** for the next steps: the identity's client ID and role name,
   the database's host name, and the Application Insights connection
   string, which with local authentication disabled is an address, not a
   secret.

## Consequences

**Positive:**

- No password or key for the database or telemetry exists anywhere: not
  in the templates, the repository, GitHub or the containers.
- The database can't be reached from the internet at all, not even with a
  valid token.
- The whole environment is reviewed code, checked on every pull request,
  and deleted with one command.

**Negative:**

- **No direct access from the laptop.** `psql` from home can't reach a
  private server; looking into the database needs a container inside the
  network.
- **The apps identity is the database administrator**: the running
  application could drop tables. A separate migration identity with an
  application role of fewer rights is the stricter setup.
- **Private access is permanent**: a server can't be moved to public
  access or to another subnet later; it has to be recreated.
- **Role assignments need more than Contributor**: whoever deploys must
  be allowed to assign roles (Owner, or Role Based Access Control
  Administrator).
- **The daily cap drops telemetry** past 1 GB in a day rather than
  storing it.

## Alternatives Considered

- **Public access with the "allow Azure services" firewall rule** --
  simpler, but it admits connections from any Azure customer's resources,
  not only ours; the token would be the only protection.
- **A password kept in Key Vault** -- the same approach as on AWS, but a
  secret to generate, store, rotate and inject, where a token needs none.
- **Terraform** -- cloud-neutral, but a state file to keep somewhere; ARM
  keeps the state of a Bicep deployment itself.
- **Azure Verified Modules** -- Microsoft's prebuilt Bicep modules for
  each service; complete, but they hide the properties this sprint is
  meant to learn.
- **A system-assigned identity per container app** -- created with each
  app and deleted with it, so the database couldn't trust it before the
  app exists, and every app would need its own grants.
- **Creating the database in the template** -- the server would create it
  as a role of its own choosing, and since PostgreSQL 15 only a database's
  owner may create tables in its `public` schema unless granted otherwise.
  Letting the migrations create it makes the identity the owner, with no
  grants to add.
