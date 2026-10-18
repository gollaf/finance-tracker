# AWS runbook

Commands for the timeboxed copy on AWS: deploying, tearing down, checking
that nothing is left running, and cleaning up when the sprint ends. Why
things are set up this way is in ADRs
[0032](./adr/0032-aws-deployment-with-cdk-fargate-and-rds.md) to
[0036](./adr/0036-aws-deploys-from-the-laptop.md).

Commands run on the laptop in `deploy/aws`, signed in with `aws login`
(run it again when a command says the credentials have expired).
Placeholders: `<sha>` is the first 7 characters of a master commit whose
CI run has finished, `<account>` the AWS account ID.

## The stacks

| Stack | Holds | Costs while it exists |
|---|---|---|
| `FinanceTracker-Data` | network, PostgreSQL | the database, by the hour |
| `FinanceTracker-App` | ECS services, load balancer, CloudFront, alarms, log groups | the load balancer and containers, by the hour |
| `CDKToolkit` | what `cdk bootstrap` created: the CDK's roles and an S3 bucket | cents (bucket storage) |

## Deploying

```bash
npx cdk diff -c imageTag=sha-<sha>                    # what would change
npx cdk deploy --all -c imageTag=sha-<sha>
```

The CLI asks before applying security-related changes (IAM, security
groups); answer `y` after reading them. The first deploy, which creates
the database and the CloudFront distribution, takes longest; it prints the
Api's address as `ApiUrl`. Images exist only for master commits, so a tag
from a branch fails after ECS retries for several minutes. To check a tag
first:

```bash
docker manifest inspect ghcr.io/gollaf/finance-tracker-api:sha-<sha>
```

## Tearing down

| Goal | Command |
|---|---|
| Stop most of the hourly cost, keep the data | `npx cdk destroy FinanceTracker-App -c imageTag=sha-<sha>` |
| Remove everything | `npx cdk destroy FinanceTracker-Data -c imageTag=sha-<sha>` |

Any valid tag will do: without one the application stack isn't part of
the app. With it, destroying the data stack destroys the application
stack first, as it depends on the data stack. Never destroy the data
stack without the tag while the application stack still exists: the
references between them don't stop CloudFormation, and the services would
lose their database.

Afterwards, check that nothing of the project is left:

```bash
aws cloudformation list-stacks --query "StackSummaries[?StackStatus!='DELETE_COMPLETE'].[StackName,StackStatus]" --output table
aws logs describe-log-groups --log-group-name-prefix /finance-tracker --query "logGroups[].logGroupName"
```

Only `CDKToolkit` should be listed (and `FinanceTracker-Data` if it was
kept), and no log groups. Billing -> **Bills** shows whether anything is
still charging.

## At the end of the sprint

1. Remove everything (above), and check.
2. Remove the bootstrap: `aws cloudformation delete-stack --stack-name CDKToolkit`.
   Its S3 bucket, `cdk-hnb659fds-assets-<account>-eu-north-1`, is kept by
   design: in the S3 console select it -> **Empty**, then **Delete** (the
   bucket is versioned, so `aws s3 rb --force` doesn't empty it).
3. Delete `~/.cdk.json` if it only held `alarmEmail`.
4. Keep the budget alert until a month's bill shows nothing.

## When something breaks

| Symptom | Look at | Usual cause |
|---|---|---|
| The CLI's progress output stops with `ENOBUFS` (Windows) | CloudFormation -> the stack -> **Events** | Only the CLI's display broke; the deploy goes on in AWS. Follow it in the console |
| Deploy fails, ECS circuit breaker in the events | `aws logs tail /finance-tracker/api --since 30m` (or `worker`, `rabbitmq`) | The new build doesn't start, or its image doesn't exist; ECS went back to the previous version |
| A stack is in `ROLLBACK_COMPLETE` | the stack's **Events**, the first `CREATE_FAILED` | Its first creation failed; it can only be deleted (`npx cdk destroy <stack>`), then deployed again |
| `DELETE_FAILED` | the stack's **Events**, the first failed resource | Usually a resource still in use for a moment; run the destroy again |
| `... with an explicit deny in a service control policy` | -- | A guardrail of the account's AWS-managed organization; nothing inside the account can allow it ([ADR 0036](./adr/0036-aws-deploys-from-the-laptop.md)) |
