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
            _template.ResourceCountIs("AWS::ECS::TaskDefinition", 3);
            _template.ResourcePropertiesCountIs("AWS::ECS::TaskDefinition", new Dictionary<string, object>
            {
                ["RuntimePlatform"] = new Dictionary<string, object>
                {
                    ["CpuArchitecture"] = "ARM64",
                    ["OperatingSystemFamily"] = "LINUX",
                },
            }, 3);
        }

        [Fact]
        public void Worker_StartsOnlyAfterMigrationsSucceeded()
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                MigrateContainer(),
                new Dictionary<string, object>
                {
                    ["Name"] = "worker",
                    ["Image"] = $"ghcr.io/gollaf/finance-tracker-worker:{AppStackFixture.ImageTag}",
                    ["DependsOn"] = DependsOnMigrate(),
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
                        Variable("RabbitMq__Host", "rabbitmq.finance-tracker.local"),
                    }),
                }));
        }

        [Fact]
        public void Api_StartsOnlyAfterMigrationsSucceeded()
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                MigrateContainer(),
                new Dictionary<string, object>
                {
                    ["Name"] = "api",
                    ["Image"] = $"ghcr.io/gollaf/finance-tracker-api:{AppStackFixture.ImageTag}",
                    ["DependsOn"] = DependsOnMigrate(),
                }));
        }

        [Fact]
        public void Api_TrustsOnlyTheVpcToNameTheVisitor_InCloudFrontsHeaders()
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", Containers(
                new Dictionary<string, object>
                {
                    ["Name"] = "api",
                    ["Environment"] = Match.ArrayWith(new object[]
                    {
                        Variable("ReverseProxy__TrustedNetworks__0", "10.20.0.0/16"),
                        Variable("ReverseProxy__ForwardedForHeader", "CloudFront-Viewer-Address"),
                        Variable("ReverseProxy__ForwardedProtoHeader", "CloudFront-Forwarded-Proto"),
                    }),
                }));
        }

        [Fact]
        public void SingleTaskServices_StopTheOldTaskFirst_AndRollBackFailedDeploys()
        {
            _template.ResourceCountIs("AWS::ECS::Service", 3);
            _template.ResourcePropertiesCountIs("AWS::ECS::Service", new Dictionary<string, object>
            {
                ["DesiredCount"] = 1,
                ["DeploymentConfiguration"] = Deployment(minimumHealthyPercent: 0, maximumPercent: 100),
            }, 2);
        }

        [Fact]
        public void Api_StartsTheNewTaskFirst_BehindTheLoadBalancer()
        {
            _template.HasResourceProperties("AWS::ECS::Service", new Dictionary<string, object>
            {
                ["DesiredCount"] = 1,
                ["DeploymentConfiguration"] = Deployment(minimumHealthyPercent: 100, maximumPercent: 200),
                ["LoadBalancers"] = new object[]
                {
                    Match.ObjectLike(new Dictionary<string, object> { ["ContainerName"] = "api", ["ContainerPort"] = 8080 }),
                },
            });
        }

        [Fact]
        public void LoadBalancer_IsInternal_InTheIsolatedSubnets()
        {
            _template.HasResourceProperties("AWS::ElasticLoadBalancingV2::LoadBalancer", new Dictionary<string, object>
            {
                ["Scheme"] = "internal",
                ["Subnets"] = new object[] { DataStackOutput("NetworkDataSubnet1"), DataStackOutput("NetworkDataSubnet2") },
            });
        }

        [Fact]
        public void LoadBalancer_SendsTrafficOnlyToReadyTasks()
        {
            _template.HasResourceProperties("AWS::ElasticLoadBalancingV2::TargetGroup", new Dictionary<string, object>
            {
                ["Port"] = 8080,
                ["TargetType"] = "ip",
                ["HealthCheckPath"] = "/health/ready",
            });
        }

        [Fact]
        public void CloudFront_ServesHttpsOnly_CachesNothing_AndReachesTheLoadBalancerPrivately()
        {
            _template.HasResourceProperties("AWS::CloudFront::Distribution", new Dictionary<string, object>
            {
                ["DistributionConfig"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["IPV6Enabled"] = false,
                    ["DefaultCacheBehavior"] = Match.ObjectLike(new Dictionary<string, object>
                    {
                        ["ViewerProtocolPolicy"] = "redirect-to-https",
                        // The managed CachingDisabled and AllViewerAndCloudFrontHeaders-2022-06 policies.
                        ["CachePolicyId"] = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad",
                        ["OriginRequestPolicyId"] = "33f36d7e-f396-46d9-90e0-52428a34d9dc",
                    }),
                    ["Origins"] = new object[]
                    {
                        Match.ObjectLike(new Dictionary<string, object> { ["VpcOriginConfig"] = Match.AnyValue() }),
                    },
                }),
            });
            _template.HasResourceProperties("AWS::CloudFront::VpcOrigin", new Dictionary<string, object>
            {
                ["VpcOriginEndpointConfig"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["OriginProtocolPolicy"] = "http-only",
                }),
            });
        }

        [Fact]
        public void Firewall_AdmitsOnlyTheIntendedClients()
        {
            // No security group has inline inbound rules (no "from anywhere");
            // these five are the only ones.
            _template.ResourcePropertiesCountIs("AWS::EC2::SecurityGroup", new Dictionary<string, object>
            {
                ["SecurityGroupIngress"] = Match.Absent(),
            }, 4);
            _template.ResourceCountIs("AWS::EC2::SecurityGroupIngress", 5);

            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", Ingress(
                port: 80, group: GroupIdOf("^LoadBalancerSecurityGroup"), source: new Dictionary<string, object>
                {
                    ["Fn::GetAtt"] = new object[]
                    {
                        Match.StringLikeRegexp("^VpcOriginsSecurityGroupLookup"), "SecurityGroups.0.GroupId",
                    },
                }));
            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", Ingress(
                port: 8080, group: GroupIdOf("^ApiSecurityGroup"), source: GroupIdOf("^LoadBalancerSecurityGroup")));
            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", Ingress(
                port: 5672, group: GroupIdOf("^RabbitMqSecurityGroup"), source: GroupIdOf("^WorkerSecurityGroup")));
            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", Ingress(
                port: 5432, group: null, source: GroupIdOf("^WorkerSecurityGroup")));
            _template.HasResourceProperties("AWS::EC2::SecurityGroupIngress", Ingress(
                port: 5432, group: null, source: GroupIdOf("^ApiSecurityGroup")));
        }

        [Fact]
        public void Logs_AreKeptForOneWeek_AndDeletedWithTheStack()
        {
            // One per service, and one for the security group lookup.
            _template.ResourceCountIs("AWS::Logs::LogGroup", 4);
            _template.ResourcePropertiesCountIs("AWS::Logs::LogGroup", new Dictionary<string, object>
            {
                ["RetentionInDays"] = 7,
            }, 4);
            _template.AllResources("AWS::Logs::LogGroup", new Dictionary<string, object>
            {
                ["DeletionPolicy"] = "Delete",
            });
        }

        [Fact]
        public void Outputs_TheApiUrl()
        {
            _template.HasOutput("ApiUrl", new Dictionary<string, object>());
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

        // A task definition whose containers include ones like these, in this order.
        private static Dictionary<string, object> Containers(params Dictionary<string, object>[] containers) => new()
        {
            ["ContainerDefinitions"] = Match.ArrayWith(containers.Select(c => (object)Match.ObjectLike(c)).ToArray()),
        };

        private static Dictionary<string, object> MigrateContainer() => new()
        {
            ["Name"] = "migrate",
            ["Image"] = $"ghcr.io/gollaf/finance-tracker-api:{AppStackFixture.ImageTag}",
            ["Command"] = new object[] { "migrate" },
            ["Essential"] = false,
        };

        private static object[] DependsOnMigrate() => new object[]
        {
            new Dictionary<string, object> { ["Condition"] = "SUCCESS", ["ContainerName"] = "migrate" },
        };

        private static Dictionary<string, object> Variable(string name, string value) => new()
        {
            ["Name"] = name,
            ["Value"] = value,
        };

        private static Dictionary<string, object> Deployment(int minimumHealthyPercent, int maximumPercent) => new()
        {
            ["MinimumHealthyPercent"] = minimumHealthyPercent,
            ["MaximumPercent"] = maximumPercent,
            ["DeploymentCircuitBreaker"] = new Dictionary<string, object> { ["Enable"] = true, ["Rollback"] = true },
        };

        // A value the data stack publishes, read with Fn::GetStackOutput.
        private static Dictionary<string, object> DataStackOutput(string outputNamePattern) => new()
        {
            ["Fn::GetStackOutput"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["OutputName"] = Match.StringLikeRegexp(outputNamePattern),
            }),
        };

        private static Dictionary<string, object> Ingress(int port, object? group, object source)
        {
            var rule = new Dictionary<string, object>
            {
                ["FromPort"] = port,
                ["ToPort"] = port,
                ["SourceSecurityGroupId"] = source,
            };
            if (group is not null)
                rule["GroupId"] = group;

            return rule;
        }

        private static Dictionary<string, object> GroupIdOf(string logicalIdPattern) => new()
        {
            ["Fn::GetAtt"] = new object[] { Match.StringLikeRegexp(logicalIdPattern), "GroupId" },
        };
    }
}
