using Amazon.CDK.Assertions;

namespace FinanceTracker.Aws.Tests
{
    public sealed class DataStackTests : IClassFixture<DataStackFixture>
    {
        private readonly Template _template;

        public DataStackTests(DataStackFixture fixture) => _template = fixture.Template;

        [Fact]
        public void Network_HasNoNatGateway()
        {
            _template.ResourceCountIs("AWS::EC2::NatGateway", 0);
        }

        [Fact]
        public void Network_OnlyThePublicSubnetsReachTheInternet()
        {
            _template.ResourcePropertiesCountIs("AWS::EC2::Subnet", Subnet("Public", mapsPublicIp: true), 2);
            _template.ResourcePropertiesCountIs("AWS::EC2::Subnet", Subnet("Isolated", mapsPublicIp: false), 2);

            // One default route to the internet gateway per public subnet; the
            // isolated subnets have none.
            _template.ResourceCountIs("AWS::EC2::Route", 2);
        }

        [Fact]
        public void Database_StaysWithinTheFreePlanLimits()
        {
            _template.HasResourceProperties("AWS::RDS::DBInstance", new Dictionary<string, object>
            {
                ["Engine"] = "postgres",
                ["EngineVersion"] = "16",
                ["DBInstanceClass"] = "db.t4g.micro",
                ["AllocatedStorage"] = "20",
                ["MaxAllocatedStorage"] = Match.Absent(),
                ["BackupRetentionPeriod"] = 1,
                ["MultiAZ"] = false,
            });
        }

        [Fact]
        public void Database_IsNotReachableFromTheInternet()
        {
            _template.HasResourceProperties("AWS::RDS::DBInstance", new Dictionary<string, object>
            {
                ["PubliclyAccessible"] = false,
                ["StorageEncrypted"] = true,
            });
            _template.HasResourceProperties("AWS::RDS::DBSubnetGroup", new Dictionary<string, object>
            {
                ["SubnetIds"] = new object[] { RefTo("^NetworkDataSubnet1"), RefTo("^NetworkDataSubnet2") },
            });
        }

        [Fact]
        public void Database_AdmitsNoClientsYet()
        {
            _template.ResourceCountIs("AWS::EC2::SecurityGroupIngress", 0);
            _template.HasResourceProperties("AWS::EC2::SecurityGroup", new Dictionary<string, object>
            {
                ["SecurityGroupIngress"] = Match.Absent(),
            });
        }

        [Fact]
        public void Database_PasswordIsGeneratedIntoSecretsManager()
        {
            _template.HasResourceProperties("AWS::SecretsManager::Secret", new Dictionary<string, object>
            {
                ["GenerateSecretString"] = new Dictionary<string, object>
                {
                    ["SecretStringTemplate"] = "{\"username\":\"financetracker\"}",
                },
            });
        }

        [Fact]
        public void DestroyingTheStack_LeavesNothingBehind()
        {
            _template.HasResource("AWS::RDS::DBInstance", new Dictionary<string, object>
            {
                ["DeletionPolicy"] = "Delete",
                ["Properties"] = new Dictionary<string, object>
                {
                    ["DeletionProtection"] = false,
                    ["DeleteAutomatedBackups"] = true,
                },
            });
            _template.HasResource("AWS::SecretsManager::Secret", new Dictionary<string, object>
            {
                ["DeletionPolicy"] = "Delete",
            });
        }

        [Fact]
        public void Outputs_TheDatabaseEndpointAndSecret()
        {
            _template.HasOutput("DatabaseEndpoint", new Dictionary<string, object>());
            _template.HasOutput("DatabaseSecretArn", new Dictionary<string, object>());
        }

        // The CDK tags every subnet with its type.
        private static Dictionary<string, object> Subnet(string type, bool mapsPublicIp) => new()
        {
            ["MapPublicIpOnLaunch"] = mapsPublicIp,
            ["Tags"] = Match.ArrayWith(new object[]
            {
                new Dictionary<string, object> { ["Key"] = "aws-cdk:subnet-type", ["Value"] = type },
            }),
        };

        // Logical IDs start with the construct path: Network, then the subnet group's name.
        private static Dictionary<string, object> RefTo(string logicalIdPattern) => new()
        {
            ["Ref"] = Match.StringLikeRegexp(logicalIdPattern),
        };
    }
}
