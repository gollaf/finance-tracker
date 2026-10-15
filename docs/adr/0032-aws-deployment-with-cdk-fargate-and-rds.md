# 32. AWS deployment with the CDK, Fargate and RDS

## Status

Accepted.

## Context

The roadmap includes a timeboxed AWS sprint: the same system on AWS's
managed services, to compare the platforms. The Oracle server stays the
permanent demo (ADR 0025); the AWS copy exists while it is being worked on
and is deleted afterwards.

The account is on AWS's free plan: a credit balance instead of an
always-free tier, and no card charged. The plan enforces limits of its
own: databases only in the micro instance classes, at most one day of
automated backups, no storage autoscaling. Anything billed by the hour
uses up credit whether or not anyone is using it.

The system needs PostgreSQL, RabbitMQ, the Api behind a public address,
the Worker, and the migration step that runs before the Api starts
(ADR 0017).

## Decision

1. **Infrastructure as code with the AWS CDK, in C#**, in `deploy/aws`.
   The CDK app synthesizes CloudFormation templates; CloudFormation
   creates, changes and deletes the resources of each template as one
   unit, a stack. `tests/FinanceTracker.Aws.Tests` checks the synthesized
   templates, with the same feature flags as `deploy/aws/cdk.json`. The
   CDK CLI version is pinned in `deploy/aws/package.json`.
2. **Containers on ECS with Fargate**: AWS runs each container without a
   server to manage. The images are the ones CI publishes to ghcr.io
   (ADR 0022); they are not copied into a registry on AWS.
3. **RabbitMQ as a container** on ECS rather than Amazon MQ.
4. **PostgreSQL on RDS**: PostgreSQL 16 on `db.t4g.micro`, 20 GB of fixed
   storage, one availability zone, encrypted, one day of automated
   backups. The password is generated into Secrets Manager and appears
   neither in the template nor in the repository.
5. **An own VPC without NAT gateways.** Two availability zones (an RDS
   subnet group must span two), each with a public subnet and an isolated
   one. The isolated subnets have no route to or from the internet, and
   the database lives there. Containers run in the public subnets with
   public addresses, which is how they reach ghcr.io; security groups
   decide what may connect to them. The database's security group admits
   nothing until a client's rule is added.
6. **Two stacks.** `FinanceTracker-Data` holds the network and the
   database; `FinanceTracker-App` will hold the cluster, the services and
   the load balancer. The application stack can be destroyed whenever the
   system isn't needed, which stops most of the hourly cost, while the
   database keeps its data.
7. **Environment-agnostic stacks**: no account or region in the code. A
   stack goes to whichever account and region the CLI is signed in to
   (eu-north-1 here), and synthesizing needs no AWS credentials, so tests
   and CI can do it. The availability zones are picked at deploy time.
8. **Throwaway data.** The database and its secret are deleted with the
   stack: no final snapshot, automated backups deleted, no deletion
   protection.
9. **Every resource is tagged** `project=finance-tracker`.

## Consequences

**Positive:**

- The whole AWS environment is reviewed code; creating or deleting it is
  one command per stack.
- The largest hourly costs a tutorial setup brings -- NAT gateways, a
  Multi-AZ database, larger instances -- are absent.
- The tests catch a change that would break a free plan limit, open the
  database to the internet or keep data after teardown, before anything
  is deployed.

**Negative:**

- **Destroying the data stack deletes the data**, with no snapshot. That
  suits a copy for learning; the real data stays on the Oracle server.
- **One availability zone**: if it fails, the database is down until it
  is back.
- **Public addresses on the containers.** AWS bills every public IPv4
  address by the hour, and the containers depend on their security
  groups alone.
- **Node.js is required** to synthesize -- the CDK runs on it -- so also
  for `dotnet test`.
- **The CDK bootstrap stack** (`CDKToolkit`: an S3 bucket, a container
  registry and IAM roles) is created once per account and region and has
  to be deleted separately at teardown.
- **No lookups of existing resources** (an existing VPC, say), which an
  environment-agnostic stack can't do; none are needed.

## Alternatives Considered

- **Terraform or OpenTofu** -- popular and cloud-neutral, but another
  language and a state file to keep somewhere; CloudFormation keeps the
  state for the CDK.
- **Plain CloudFormation templates** -- what the CDK produces anyway, at
  several times the length and without a compiler.
- **App Runner** -- simpler hosting, but for web services only; the
  Worker and RabbitMQ don't fit it.
- **EKS** -- Kubernetes is already covered locally (ADR 0019), and the
  cluster itself bills by the hour on top of what runs in it.
- **Amazon MQ** -- a managed RabbitMQ, but its broker bills around the
  clock, where the container runs only while the application stack
  exists.
- **Private subnets behind a NAT gateway** -- the textbook layout, but a
  NAT gateway bills per hour and per gigabyte, in each zone that has one.
- **The default VPC** -- already in every account, but all its subnets
  are public: the database would be one setting away from the internet.
