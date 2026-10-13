using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Api.IntegrationTests.OpenApi
{
    /// <summary>
    /// Boots the real Api host in the Production environment, without a
    /// database: startup migrations are off, and the tests using this factory
    /// only call endpoints that never open a connection.
    /// </summary>
    public sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
    {
        public ProductionWebApplicationFactory()
        {
            // AddInfrastructure needs a connection string before Build() (see
            // CustomWebApplicationFactory). Nothing ever connects with this one.
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__FinanceTracker",
                "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false"
                });
            });
        }
    }
}
