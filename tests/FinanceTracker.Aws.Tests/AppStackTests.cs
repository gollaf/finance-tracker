using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace FinanceTracker.Aws.Tests
{
    public sealed class AppStackTests : IClassFixture<AppStackFixture>
    {
        private readonly Template _template;

        public AppStackTests(AppStackFixture fixture) => _template = fixture.Template;

        [Fact]
        public void Cluster_HasAPrivateDnsNamespace()
        {
            _template.HasResourceProperties("AWS::ServiceDiscovery::PrivateDnsNamespace", new Dictionary<string, object>
            {
                ["Name"] = "finance-tracker.local",
            });
        }

        [Fact]
        public void Tasks_RunOnArm64()
        {
            _template.ResourceCountIs("AWS::ECS::TaskDefinition", 2);
            _template.ResourcePropertiesCountIs("AWS::ECS::TaskDefinition", new Dictionary<string, object>
            {
                ["RuntimePlatform"] = new Dictionary<string, object>
                {
                    ["CpuArchitecture"] = "ARM64",
                    ["OperatingSystemFamily"] = "LINUX",
                },
            }, 2);
        }

        [Fact]
        public void Worker_StartsOnlyAfterMigrationsSucceeded()
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                new Dictionary<string, object>
                {
                    ["Name"] = "migrate",
                    ["Image"] = $"ghcr.io/gollaf/finance-tracker-api:{AppStackFixture.ImageTag}",
                    ["Command"] = new object[] { "migrate" },
                    ["Essential"] = false,
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "worker",
                    ["Image"] = $"ghcr.io/gollaf/finance-tracker-worker:{AppStackFixture.ImageTag}",
                    ["DependsOn"] = new object[]
                    {
                        new Dictionary<string, object> { ["Condition"] = "SUCCESS", ["ContainerName"] = "migrate" },
                    },
                }));
        }

        [Fact]
        public void Worker_GetsItsPasswordsFromSecretsManager()
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                new Dictionary<string, object>
                {
                    ["Name"] = "worker",
                    ["Secrets"] = Match.ArrayWith(new object[]
                    {
                        Match.ObjectLike(new Dictionary<string, object> { ["Name"] = "PGPASSWORD" }),
                        Match.ObjectLike(new Dictionary<string, object> { ["Name"] = "RabbitMq__Password" }),
                    }),
                }));
        }

        [Fact]
        public void Worker_FindsRabbitMqByItsDnsName()
        {
            _template.HasResourceProperties("AWS::ServiceDiscovery::Service", new Dictionary<string, object>
            {
                ["Name"] = "rabbitmq",
            });
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                new Dictionary<string, object>
                {
                    ["Name"] = "worker",
                    ["Environment"] = Match.ArrayWith(new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["Name"] = "RabbitMq__Host",
                            ["Value"] = "rabbitmq.finance-tracker.local",
                        },
                    }),
                }));
        }

        [Fact]
        public void Services_RunOneTaskAndRollBackFailedDeploys()
        {
            _template.ResourceCountIs("AWS::ECS::Service", 2);
            _template.ResourcePropertiesCountIs("AWS::ECS::Service", new Dictionary<string, object>
            {
                ["DesiredCount"] = 1,
                ["DeploymentConfiguration"] = new Dictionary<string, object>
                {
                    ["MaximumPercent"] = 100,
                    ["MinimumHealthyPercent"] = 0,
                    ["DeploymentCircuitBreaker"] = new Dictionary<string, object> { ["Enable"] = true, ["Rollback"] = true },
                },
            }, 2);
        }

        [Fact]
        public void Firewall_AdmitsOnlyTheWorker()
        {
            // No security group has inline inbound rules; the only ones are these two.
            _template.ResourcePropertiesCountIs("AWS::EC2::SecurityGroup", new Dictionary<string, object>
            {
                ["SecurityGroupIngress"] = Match.Absent(),
            }, 2);
            _template.ResourceCountIs("AWS::EC2::SecurityGroupIngress", 2);

            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", new Dictionary<string, object>
            {
                ["FromPort"] = 5672,
                ["ToPort"] = 5672,
                ["GroupId"] = GroupIdOf("^RabbitMqSecurityGroup"),
                ["SourceSecurityGroupId"] = GroupIdOf("^WorkerSecurityGroup"),
            });
            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", new Dictionary<string, object>
            {
                ["FromPort"] = 5432,
                ["ToPort"] = 5432,
                ["SourceSecurityGroupId"] = GroupIdOf("^WorkerSecurityGroup"),
            });
        }

        [Fact]
        public void Logs_AreKeptForOneWeek()
        {
            _template.ResourceCountIs("AWS::Logs::LogGroup", 2);
            _template.ResourcePropertiesCountIs("AWS::Logs::LogGroup", new Dictionary<string, object>
            {
                ["RetentionInDays"] = 7,
            }, 2);
        }

        [Theory]
        [InlineData("latest")]
        [InlineData("sha-123")]
        [InlineData("sha-ABCDEF1")]
        [InlineData("1234567")]
        public void ImageTag_OtherThanAPublishedShaTag_IsRefused(string imageTag)
        {
            var app = new App();
            var data = new DataStack(app, "Data");

            var create = () => new AppStack(app, "App", new AppStackProps { Data = data, ImageTag = imageTag });

            var exception = Assert.Throws<ArgumentException>(create);
            Assert.Contains(imageTag, exception.Message);
        }

        // A task definition whose containers include ones like these.
        private static Dictionary<string, object> Containers(params Dictionary<string, object>[] containers) => new()
        {
            ["ContainerDefinitions"] = Match.ArrayWith(containers.Select(c => (object)Match.ObjectLike(c)).ToArray()),
        };

        private static Dictionary<string, object> GroupIdOf(string logicalIdPattern) => new()
        {
            ["Fn::GetAtt"] = new object[] { Match.StringLikeRegexp(logicalIdPattern), "GroupId" },
        };
    }
}
