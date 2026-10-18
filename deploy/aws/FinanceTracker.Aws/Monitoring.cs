using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;
using Distribution = Amazon.CDK.AWS.CloudFront.Distribution;

namespace FinanceTracker.Aws
{
    /// <summary>What the monitoring watches: the application stack's parts and the database.</summary>
    public sealed record MonitoredSystem(
        DataStack Data,
        FargateService Api,
        FargateService Worker,
        FargateService RabbitMq,
        ApplicationTargetGroup ApiTargets,
        Distribution Distribution,
        LogGroup ApiLogs,
        LogGroup WorkerLogs,
        string? AlarmEmail);

    /// <summary>
    /// CloudWatch on top of the system: a metric for every error the .NET
    /// services log, alarms that send an e-mail when something breaks and
    /// again when it recovers, a dashboard, and a saved Logs Insights query.
    /// </summary>
    public sealed class Monitoring : Construct
    {
        public const string MetricNamespace = "FinanceTracker";

        public Monitoring(Construct scope, string id, MonitoredSystem system)
            : base(scope, id)
        {
            var topic = new Topic(this, "Alarms", new TopicProps { DisplayName = "Finance Tracker alarms" });
            // Without an address the alarms still change state on the
            // dashboard; they just tell no one. AWS sends a confirmation
            // e-mail first, and nothing arrives until its link is clicked.
            if (system.AlarmEmail is not null)
                topic.AddSubscription(new EmailSubscription(system.AlarmEmail));

            var apiErrors = ErrorsLogged(system.ApiLogs, "ApiErrors");
            var workerErrors = ErrorsLogged(system.WorkerLogs, "WorkerErrors");
            var targets = system.ApiTargets.Metrics;

            var alarms = new IAlarm[]
            {
                NewAlarm(topic, "ApiUnreachable",
                    "No healthy Api task behind the load balancer for 3 minutes",
                    targets.HealthyHostCount(new MetricOptions { Statistic = "Minimum", Period = Duration.Minutes(1) }),
                    ComparisonOperator.LESS_THAN_THRESHOLD, threshold: 1, evaluationPeriods: 3,
                    // No data at all means nothing is reporting healthy either.
                    TreatMissingData.BREACHING),
                NewAlarm(topic, "ApiServerErrors",
                    "The Api answered 5 or more requests with a 5xx status in 5 minutes",
                    targets.HttpCodeTarget(HttpCodeTarget.TARGET_5XX_COUNT, new MetricOptions
                    {
                        Statistic = "Sum",
                        Period = Duration.Minutes(5),
                    }),
                    ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD, threshold: 5, evaluationPeriods: 1,
                    // A count with no data points means no errors.
                    TreatMissingData.NOT_BREACHING),
                NewAlarm(topic, "ApiErrorsLogged", "The Api logged an error",
                    apiErrors, ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD, threshold: 1, evaluationPeriods: 1,
                    TreatMissingData.NOT_BREACHING),
                NewAlarm(topic, "WorkerErrorsLogged", "The Worker logged an error",
                    workerErrors, ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD, threshold: 1, evaluationPeriods: 1,
                    TreatMissingData.NOT_BREACHING),
                NewAlarm(topic, "DatabaseStorageLow", "Less than 2 GiB free on the database",
                    system.Data.Database.MetricFreeStorageSpace(new MetricOptions
                    {
                        Statistic = "Minimum",
                        Period = Duration.Minutes(15),
                    }),
                    ComparisonOperator.LESS_THAN_THRESHOLD, threshold: 2d * 1024 * 1024 * 1024, evaluationPeriods: 1,
                    TreatMissingData.MISSING),
            };

            var dashboard = new Dashboard(this, "Dashboard", new DashboardProps
            {
                DashboardName = "FinanceTracker",
                DefaultInterval = Duration.Hours(3),
            });
            dashboard.AddWidgets(new AlarmStatusWidget(new AlarmStatusWidgetProps
            {
                Title = "Alarms",
                Alarms = alarms,
                Width = 24,
                Height = 3,
            }));
            dashboard.AddWidgets(
                Graph("CloudFront requests", CloudFront(system.Distribution, "Requests", "Sum")),
                Graph("CloudFront 5xx error rate (%)", CloudFront(system.Distribution, "5xxErrorRate", "Average")),
                Graph("Api response time, p95 (s)", targets.TargetResponseTime(new MetricOptions { Statistic = "p95" })));
            dashboard.AddWidgets(
                Graph("CPU (%)",
                    system.Api.MetricCpuUtilization(), system.Worker.MetricCpuUtilization(), system.RabbitMq.MetricCpuUtilization()),
                Graph("Memory (%)",
                    system.Api.MetricMemoryUtilization(), system.Worker.MetricMemoryUtilization(), system.RabbitMq.MetricMemoryUtilization()),
                Graph("Errors logged", apiErrors, workerErrors));
            dashboard.AddWidgets(
                Graph("Database CPU (%)", system.Data.Database.MetricCPUUtilization()),
                Graph("Database connections", system.Data.Database.MetricDatabaseConnections()),
                Graph("Database free storage (bytes)", system.Data.Database.MetricFreeStorageSpace()));

            // The services log JSON, so Logs Insights can filter on its fields.
            _ = new QueryDefinition(this, "ErrorsQuery", new QueryDefinitionProps
            {
                QueryDefinitionName = "finance-tracker/errors",
                LogGroups = new ILogGroup[] { system.ApiLogs, system.WorkerLogs },
                QueryString = new QueryString(new QueryStringProps
                {
                    Fields = new[] { "@timestamp", "@logStream", "Category", "Message" },
                    FilterStatements = new[] { "LogLevel in [\"Error\", \"Critical\"]" },
                    Sort = "@timestamp desc",
                    Limit = 50,
                }),
            });
        }

        // Counts the log lines at Error or Critical level, read from the
        // JSON the console logger writes.
        private IMetric ErrorsLogged(LogGroup logGroup, string metricName)
        {
            var filter = new MetricFilter(this, $"{metricName}Filter", new MetricFilterProps
            {
                LogGroup = logGroup,
                MetricNamespace = MetricNamespace,
                MetricName = metricName,
                FilterPattern = FilterPattern.Any(
                    FilterPattern.StringValue("$.LogLevel", "=", "Error"),
                    FilterPattern.StringValue("$.LogLevel", "=", "Critical")),
                MetricValue = "1",
                // A zero for quiet periods, so the graph shows a line rather than gaps.
                DefaultValue = 0,
            });

            return filter.Metric(new MetricOptions { Statistic = "Sum", Period = Duration.Minutes(5) });
        }

        // Notifies both ways: when the alarm goes off and when it clears.
        private IAlarm NewAlarm(
            Topic topic, string id, string description, IMetric metric, ComparisonOperator comparison,
            double threshold, double evaluationPeriods, TreatMissingData missingData)
        {
            var alarm = new Alarm(this, id, new AlarmProps
            {
                AlarmDescription = description,
                Metric = metric,
                ComparisonOperator = comparison,
                Threshold = threshold,
                EvaluationPeriods = evaluationPeriods,
                TreatMissingData = missingData,
            });
            alarm.AddAlarmAction(new SnsAction(topic));
            alarm.AddOkAction(new SnsAction(topic));
            return alarm;
        }

        // CloudFront publishes its metrics only in us-east-1, always with
        // Region=Global; the CDK's own CloudFront metrics leave out both.
        private static IMetric CloudFront(Distribution distribution, string metricName, string statistic) =>
            new Metric(new MetricProps
            {
                Namespace = "AWS/CloudFront",
                MetricName = metricName,
                Statistic = statistic,
                Period = Duration.Minutes(5),
                Region = "us-east-1",
                DimensionsMap = new Dictionary<string, string>
                {
                    ["DistributionId"] = distribution.DistributionId,
                    ["Region"] = "Global",
                },
            });

        private static GraphWidget Graph(string title, params IMetric[] metrics) => new(new GraphWidgetProps
        {
            Title = title,
            Left = metrics,
            Width = 8,
        });
    }
}
