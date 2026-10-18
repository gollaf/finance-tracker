using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace FinanceTracker.Aws.Tests
{
    public sealed class MonitoringTests : IClassFixture<AppStackFixture>
    {
        private readonly Template _template;

        public MonitoringTests(AppStackFixture fixture) => _template = fixture.Template;

        [Theory]
        [InlineData("migrate")]
        [InlineData("api")]
        [InlineData("worker")]
        public void DotNetContainers_LogJson(string containerName)
        {
            _template.HasResourceProperties("AWS::ECS::TaskDefinition", new Dictionary<string, object>
            {
                ["ContainerDefinitions"] = Match.ArrayWith(new object[]
                {
                    Match.ObjectLike(new Dictionary<string, object>
                    {
                        ["Name"] = containerName,
                        ["Environment"] = Match.ArrayWith(new object[]
                        {
                            Variable("Logging__Console__FormatterName", "json"),
                            Variable("Logging__Console__FormatterOptions__IncludeScopes", "true"),
                        }),
                    }),
                }),
            });
        }

        [Fact]
        public void ErrorsInTheLogs_BecomeMetrics()
        {
            _template.ResourcePropertiesCountIs("AWS::Logs::MetricFilter", new Dictionary<string, object>
            {
                ["FilterPattern"] = "{ ($.LogLevel = \"Error\") || ($.LogLevel = \"Critical\") }",
            }, 2);
            _template.HasResourceProperties("AWS::Logs::MetricFilter", ErrorMetric("ApiErrors"));
            _template.HasResourceProperties("AWS::Logs::MetricFilter", ErrorMetric("WorkerErrors"));
        }

        [Fact]
        public void EveryAlarm_NotifiesWhenItGoesOffAndWhenItClears()
        {
            _template.ResourceCountIs("AWS::CloudWatch::Alarm", 5);
            _template.ResourcePropertiesCountIs("AWS::CloudWatch::Alarm", new Dictionary<string, object>
            {
                ["AlarmActions"] = new object[] { Match.AnyValue() },
                ["OKActions"] = new object[] { Match.AnyValue() },
            }, 5);
        }

        [Fact]
        public void Alarm_WhenNoApiTaskIsHealthy()
        {
            _template.HasResourceProperties("AWS::CloudWatch::Alarm", new Dictionary<string, object>
            {
                ["MetricName"] = "HealthyHostCount",
                ["ComparisonOperator"] = "LessThanThreshold",
                ["Threshold"] = 1,
                ["EvaluationPeriods"] = 3,
                ["TreatMissingData"] = "breaching",
            });
        }

        [Fact]
        public void Alarm_WhenTheDatabaseRunsOutOfSpace()
        {
            _template.HasResourceProperties("AWS::CloudWatch::Alarm", new Dictionary<string, object>
            {
                ["Namespace"] = "AWS/RDS",
                ["MetricName"] = "FreeStorageSpace",
                ["ComparisonOperator"] = "LessThanThreshold",
                ["Threshold"] = 2147483648,
            });
        }

        [Fact]
        public void Dashboard_ReadsCloudFrontsMetricsFromUsEast1()
        {
            _template.HasResourceProperties("AWS::CloudWatch::Dashboard", new Dictionary<string, object>
            {
                ["DashboardName"] = "FinanceTracker",
                ["DashboardBody"] = new Dictionary<string, object>
                {
                    ["Fn::Join"] = new object[]
                    {
                        "",
                        Match.ArrayWith(new object[]
                        {
                            Match.StringLikeRegexp("\"Region\",\"Global\",\\{\"region\":\"us-east-1\""),
                        }),
                    },
                },
            });
        }

        [Fact]
        public void ErrorsQuery_IsSaved()
        {
            _template.HasResourceProperties("AWS::Logs::QueryDefinition", new Dictionary<string, object>
            {
                ["Name"] = "finance-tracker/errors",
            });
        }

        [Fact]
        public void AlarmEmail_IsSubscribedToTheAlarms()
        {
            _template.HasResourceProperties("AWS::SNS::Subscription", new Dictionary<string, object>
            {
                ["Protocol"] = "email",
                ["Endpoint"] = AppStackFixture.AlarmEmail,
            });
        }

        [Fact]
        public void WithoutAnAlarmEmail_NoOneIsSubscribed()
        {
            var app = new App(new AppProps { Context = CdkContext.Load() });
            var data = new DataStack(app, "Data");
            var stack = new AppStack(app, "App", new AppStackProps { Data = data, ImageTag = AppStackFixture.ImageTag });

            Template.FromStack(stack).ResourceCountIs("AWS::SNS::Subscription", 0);
        }

        [Theory]
        [InlineData("not-an-address")]
        [InlineData("two@at@example.com")]
        [InlineData("no-domain@")]
        public void AlarmEmail_ThatIsNotAnAddress_IsRefused(string alarmEmail)
        {
            var app = new App();
            var data = new DataStack(app, "Data");

            var create = () => new AppStack(app, "App", new AppStackProps
            {
                Data = data,
                ImageTag = AppStackFixture.ImageTag,
                AlarmEmail = alarmEmail,
            });

            var exception = Assert.Throws<ArgumentException>(create);
            Assert.Contains(alarmEmail, exception.Message);
        }

        private static Dictionary<string, object> Variable(string name, string value) => new()
        {
            ["Name"] = name,
            ["Value"] = value,
        };

        private static Dictionary<string, object> ErrorMetric(string metricName) => new()
        {
            ["MetricTransformations"] = new object[]
            {
                Match.ObjectLike(new Dictionary<string, object>
                {
                    ["MetricNamespace"] = "FinanceTracker",
                    ["MetricName"] = metricName,
                    ["MetricValue"] = "1",
                }),
            },
        };
    }
}
