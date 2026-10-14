using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Api.IntegrationTests.RateLimiting
{
    /// <summary>
    /// Boots the real Api host with tiny limits that last an hour, so a few
    /// requests reach them and nothing resets during a test, and with
    /// 10.0.0.0/8 as the reverse proxy's network. No database: the tests only
    /// call endpoints that answer without one.
    /// </summary>
    public sealed class RateLimitingWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const int PermitLimit = 3;
        public const int AuthPermitLimit = 2;

        public RateLimitingWebApplicationFactory()
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
                    ["RateLimiting:PermitLimit"] = PermitLimit.ToString(),
                    ["RateLimiting:Window"] = "01:00:00",
                    ["RateLimiting:AuthPermitLimit"] = AuthPermitLimit.ToString(),
                    ["RateLimiting:AuthWindow"] = "01:00:00",
                    ["ReverseProxy:TrustedNetworks:0"] = "10.0.0.0/8",
                });
            });
        }
    }
}
