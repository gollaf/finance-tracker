using System.Text.RegularExpressions;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SecretsManager;
using Amazon.CDK.AWS.ServiceDiscovery;
using Constructs;
using EcsSecret = Amazon.CDK.AWS.ECS.Secret;
using SecretsManagerSecret = Amazon.CDK.AWS.SecretsManager.Secret;

namespace FinanceTracker.Aws
{
    /// <summary>
    /// The running system: an ECS cluster on Fargate with RabbitMQ and the
    /// Worker. It holds no data of its own, so it can be destroyed whenever
    /// the system isn't needed and deployed again from the same images.
    /// </summary>
    public sealed class AppStack : Stack
    {
        private const string Namespace = "finance-tracker.local";
        private const string RabbitMqUser = "financetracker";
        private const int RabbitMqPort = 5672;

        public AppStack(Construct scope, string id, AppStackProps props)
            : base(scope, id, Validated(props))
        {
            var data = props.Data;

            // A private DNS zone inside the VPC: a service registered as
            // "rabbitmq" is found at rabbitmq.finance-tracker.local, and the
            // record follows the task to whatever address it gets.
            var cluster = new Cluster(this, "Cluster", new ClusterProps
            {
                Vpc = data.Vpc,
                DefaultCloudMapNamespace = new CloudMapNamespaceOptions
                {
                    Name = Namespace,
                    Type = NamespaceType.DNS_PRIVATE,
                    Vpc = data.Vpc,
                },
            });

            var rabbitMqSecret = new SecretsManagerSecret(this, "RabbitMqSecret", new SecretProps
            {
                Description = "Password of the RabbitMQ user",
                // Letters and digits only, so the password never needs
                // escaping wherever it is passed on.
                GenerateSecretString = new SecretStringGenerator { ExcludePunctuation = true, PasswordLength = 32 },
                RemovalPolicy = RemovalPolicy.DESTROY,
            });

            var rabbitMq = AddRabbitMq(cluster, data, rabbitMqSecret);
            var worker = AddWorker(cluster, data, props.ImageTag, rabbitMqSecret);

            rabbitMq.Connections.AllowFrom(worker, Port.Tcp(RabbitMqPort), "AMQP from the Worker");
            AllowDatabaseFrom(data, worker, "DatabaseFromWorker", "PostgreSQL from the Worker");
        }

        private static AppStackProps Validated(AppStackProps props)
        {
            // The tag ends up in image names; only the shape CI publishes is
            // accepted, and "latest" is refused because ECS would keep
            // running whatever it pulled first.
            if (!Regex.IsMatch(props.ImageTag, "^sha-[0-9a-f]{7}$"))
            {
                throw new ArgumentException(
                    $"Expected an image tag like sha-1234567, got '{props.ImageTag}'.", nameof(props));
            }

            return props;
        }

        private FargateService AddRabbitMq(Cluster cluster, DataStack data, SecretsManagerSecret password)
        {
            var task = new FargateTaskDefinition(this, "RabbitMqTask", new FargateTaskDefinitionProps
            {
                Cpu = 256,
                MemoryLimitMiB = 1024,
                RuntimePlatform = Arm64(),
            });

            task.AddContainer("rabbitmq", new ContainerDefinitionOptions
            {
                // Docker's official image, mirrored on Amazon ECR Public:
                // no Docker Hub pull limits.
                Image = ContainerImage.FromRegistry("public.ecr.aws/docker/library/rabbitmq:4"),
                // RabbitMQ throttles publishers above a memory watermark. It
                // can't always detect a container's limit, so the watermark is
                // set to about 60% of the task's memory before it starts.
                EntryPoint = new[] { "sh", "-c" },
                Command = new[]
                {
                    "echo 'vm_memory_high_watermark.absolute = 600Mi' > /etc/rabbitmq/conf.d/90-memory.conf"
                        + " && exec docker-entrypoint.sh rabbitmq-server",
                },
                Environment = new Dictionary<string, string> { ["RABBITMQ_DEFAULT_USER"] = RabbitMqUser },
                Secrets = new Dictionary<string, EcsSecret>
                {
                    ["RABBITMQ_DEFAULT_PASS"] = EcsSecret.FromSecretsManager(password),
                },
                PortMappings = new IPortMapping[] { new PortMapping { ContainerPort = RabbitMqPort } },
                HealthCheck = new Amazon.CDK.AWS.ECS.HealthCheck
                {
                    Command = new[] { "CMD", "rabbitmq-diagnostics", "-q", "check_port_connectivity" },
                    Interval = Duration.Seconds(10),
                    Timeout = Duration.Seconds(5),
                    Retries = 10,
                    StartPeriod = Duration.Seconds(30),
                },
                Logging = Logs("rabbitmq"),
            });

            return SingleTaskService("RabbitMq", cluster, data, task, new CloudMapOptions
            {
                Name = "rabbitmq",
                // Short, so a replaced task's new address is picked up quickly.
                DnsTtl = Duration.Seconds(10),
            });
        }

        private FargateService AddWorker(Cluster cluster, DataStack data, string imageTag, SecretsManagerSecret rabbitMqPassword)
        {
            // No password here: Npgsql reads it from the PGPASSWORD
            // environment variable, which ECS fills from Secrets Manager.
            var connectionString =
                $"Host={data.Database.DbInstanceEndpointAddress};Port=5432;Database=financetracker;Username=financetracker";
            var databasePassword = EcsSecret.FromSecretsManager(data.Database.Secret!, "password");

            var task = new FargateTaskDefinition(this, "WorkerTask", new FargateTaskDefinitionProps
            {
                Cpu = 256,
                MemoryLimitMiB = 512,
                RuntimePlatform = Arm64(),
            });

            var logs = Logs("worker");

            // Runs the Api image's "migrate" command and exits; the Worker
            // starts only if it succeeded.
            var migrate = task.AddContainer("migrate", new ContainerDefinitionOptions
            {
                Image = ContainerImage.FromRegistry($"ghcr.io/gollaf/finance-tracker-api:{imageTag}"),
                Command = new[] { "migrate" },
                Essential = false,
                Environment = new Dictionary<string, string>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["ConnectionStrings__FinanceTracker"] = connectionString,
                },
                Secrets = new Dictionary<string, EcsSecret> { ["PGPASSWORD"] = databasePassword },
                Logging = logs,
            });

            var worker = task.AddContainer("worker", new ContainerDefinitionOptions
            {
                Image = ContainerImage.FromRegistry($"ghcr.io/gollaf/finance-tracker-worker:{imageTag}"),
                Environment = new Dictionary<string, string>
                {
                    ["DOTNET_ENVIRONMENT"] = "Production",
                    ["ConnectionStrings__FinanceTracker"] = connectionString,
                    ["RabbitMq__Host"] = $"rabbitmq.{Namespace}",
                    ["RabbitMq__UserName"] = RabbitMqUser,
                },
                Secrets = new Dictionary<string, EcsSecret>
                {
                    ["PGPASSWORD"] = databasePassword,
                    ["RabbitMq__Password"] = EcsSecret.FromSecretsManager(rabbitMqPassword),
                },
                // Longer than the .NET host's 30-second shutdown timeout.
                StopTimeout = Duration.Seconds(45),
                Logging = logs,
            });
            worker.AddContainerDependencies(new ContainerDependency
            {
                Container = migrate,
                Condition = ContainerDependencyCondition.SUCCESS,
            });

            return SingleTaskService("Worker", cluster, data, task, cloudMap: null);
        }

        private FargateService SingleTaskService(
            string id, Cluster cluster, DataStack data, FargateTaskDefinition task, CloudMapOptions? cloudMap)
        {
            var securityGroup = new SecurityGroup(this, $"{id}SecurityGroup", new SecurityGroupProps
            {
                Vpc = data.Vpc,
                Description = $"{id}; inbound rules are added per client",
            });

            return new FargateService(this, id, new FargateServiceProps
            {
                Cluster = cluster,
                TaskDefinition = task,
                // One task, stopped before its replacement starts: one broker,
                // and exactly one outbox relay.
                DesiredCount = 1,
                MinHealthyPercent = 0,
                MaxHealthyPercent = 100,
                // A new version that keeps failing is rolled back to the last
                // one that ran.
                CircuitBreaker = new DeploymentCircuitBreaker { Rollback = true },
                // Without a NAT gateway, a public address is how the task
                // reaches the image registries; the security group still
                // admits nothing it wasn't told to.
                AssignPublicIp = true,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                SecurityGroups = new ISecurityGroup[] { securityGroup },
                CloudMapOptions = cloudMap,
            });
        }

        // The rule is created here, not in the data stack: that stack must
        // never depend on this one, so this one can come and go.
        private void AllowDatabaseFrom(DataStack data, FargateService client, string id, string description)
        {
            _ = new CfnSecurityGroupIngress(this, id, new CfnSecurityGroupIngressProps
            {
                GroupId = data.DatabaseSecurityGroup.SecurityGroupId,
                SourceSecurityGroupId = client.Connections.SecurityGroups[0].SecurityGroupId,
                IpProtocol = "tcp",
                FromPort = 5432,
                ToPort = 5432,
                Description = description,
            });
        }

        private LogDriver Logs(string name)
        {
            var logGroup = new LogGroup(this, $"{name}-logs", new LogGroupProps
            {
                LogGroupName = $"/finance-tracker/{name}",
                Retention = RetentionDays.ONE_WEEK,
                RemovalPolicy = RemovalPolicy.DESTROY,
            });

            return LogDrivers.AwsLogs(new AwsLogDriverProps { LogGroup = logGroup, StreamPrefix = name });
        }

        // The images are built for arm64 too, and Fargate's Graviton
        // capacity costs less than x86 for the same size.
        private static RuntimePlatform Arm64() => new()
        {
            CpuArchitecture = CpuArchitecture.ARM64,
            OperatingSystemFamily = OperatingSystemFamily.LINUX,
        };
    }
}
