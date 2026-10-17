using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Api.IntegrationTests.RateLimiting
{
    /// <summary>
    /// Like RateLimitingWebApplicationFactory, but the proxy passes the
    /// visitor on under CloudFront's header names instead of the
    /// X-Forwarded-* ones.
    /// </summary>
    public sealed class ProxyHeaderNamesWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const int AuthPermitLimit = 2;
        public const string ForwardedForHeader = "CloudFront-Viewer-Address";
        public const string ForwardedProtoHeader = "CloudFront-Forwarded-Proto";

        public ProxyHeaderNamesWebApplicationFactory()
        {
            // AddInfrastructure needs a connection string before Build() (see
            // CustomWebApplicationFactory). Nothing ever connects with this one.
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__FinanceTracker",
                "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["RateLimiting:PermitLimit"] = "1000",
                    ["RateLimiting:Window"] = "01:00:00",
                    ["RateLimiting:AuthPermitLimit"] = AuthPermitLimit.ToString(),
                    ["RateLimiting:AuthWindow"] = "01:00:00",
                    ["ReverseProxy:TrustedNetworks:0"] = "10.0.0.0/8",
                    ["ReverseProxy:ForwardedForHeader"] = ForwardedForHeader,
                    ["ReverseProxy:ForwardedProtoHeader"] = ForwardedProtoHeader,
                });
            });
        }
    }
}
