using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.RDS;
using Constructs;
using InstanceType = Amazon.CDK.AWS.EC2.InstanceType;

namespace FinanceTracker.Aws
{
    /// <summary>
    /// The network and the PostgreSQL database: the parts that hold data. The
    /// application's stack is deployed into this network and can be destroyed
    /// on its own while the database keeps its data.
    /// </summary>
    public sealed class DataStack : Stack
    {
        public DataStack(Construct scope, string id, IStackProps? props = null)
            : base(scope, id, props)
        {
            var vpc = new Vpc(this, "Network", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("10.20.0.0/16"),
                // An RDS subnet group must span two availability zones, even
                // for a single-zone database.
                MaxAzs = 2,
                // A NAT gateway bills by the hour even when idle. Containers
                // get public addresses in the public subnets instead, and the
                // database needs no internet access at all.
                NatGateways = 0,
                SubnetConfiguration = new ISubnetConfiguration[]
                {
                    new SubnetConfiguration { Name = "Public", SubnetType = SubnetType.PUBLIC, CidrMask = 24 },
                    // No route to or from the internet.
                    new SubnetConfiguration { Name = "Data", SubnetType = SubnetType.PRIVATE_ISOLATED, CidrMask = 24 },
                },
            });

            // PostgreSQL only answers connections, it never opens any. Whatever
            // needs the database adds its own ingress rule.
            var securityGroup = new SecurityGroup(this, "DatabaseSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "PostgreSQL; ingress added per client",
                AllowAllOutbound = false,
            });

            var database = new DatabaseInstance(this, "Database", new DatabaseInstanceProps
            {
                Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps
                {
                    Version = PostgresEngineVersion.VER_16,
                }),
                // The free plan's limits: a micro instance class, fixed storage
                // (no MaxAllocatedStorage) and at most one day of backups.
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE4_GRAVITON, InstanceSize.MICRO),
                AllocatedStorage = 20,
                StorageType = StorageType.GP2,
                BackupRetention = Duration.Days(1),
                MultiAz = false,
                Vpc = vpc,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
                SecurityGroups = new ISecurityGroup[] { securityGroup },
                PubliclyAccessible = false,
                StorageEncrypted = true,
                DatabaseName = "financetracker",
                // The password is generated into Secrets Manager; it never
                // appears in the template or in this repository.
                Credentials = Credentials.FromGeneratedSecret("financetracker"),
                // A copy for learning, not the real data (that lives on the
                // Oracle server): deleting the stack deletes the database
                // instead of keeping a final snapshot that goes on costing.
                RemovalPolicy = RemovalPolicy.DESTROY,
                DeletionProtection = false,
                DeleteAutomatedBackups = true,
            });

            _ = new CfnOutput(this, "DatabaseEndpoint", new CfnOutputProps
            {
                Value = database.DbInstanceEndpointAddress,
                Description = "Host name of the PostgreSQL instance",
            });
            _ = new CfnOutput(this, "DatabaseSecretArn", new CfnOutputProps
            {
                Value = database.Secret!.SecretArn,
                Description = "Secrets Manager secret with the database user and password",
            });

            Vpc = vpc;
            DatabaseSecurityGroup = securityGroup;
            Database = database;
        }

        public IVpc Vpc { get; }

        /// <summary>Clients are let in by adding ingress rules to this group.</summary>
        public ISecurityGroup DatabaseSecurityGroup { get; }

        public DatabaseInstance Database { get; }
    }
}
