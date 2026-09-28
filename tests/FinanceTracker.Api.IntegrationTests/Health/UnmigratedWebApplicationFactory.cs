using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Api.IntegrationTests.Health
{
    /// <summary>
    /// Boots the real Api host the way a deployment that runs migrations as
    /// a separate step does: Database:ApplyMigrationsOnStartup is off, so
    /// the host starts against a fresh, completely empty database and stays
    /// that way until a test calls <see cref="ApplyMigrationsAsync"/> --
    /// the in-process equivalent of running the `migrate` command.
    ///
    /// The connection string uses the same environment-variable workaround
    /// as CustomWebApplicationFactory, for the same reason (see its doc
    /// comment). The migration switch doesn't need it: Program.cs reads that
    /// one after Build(), by which point ConfigureAppConfiguration below has
    /// been applied.
    /// </summary>
    public sealed class UnmigratedWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            Environment.SetEnvironmentVariable("ConnectionStrings__FinanceTracker", _postgres.GetConnectionString());

            // Builds the host now, while the environment variable above
            // still points at this factory's own container.
            _ = Services;
        }

        public async Task ApplyMigrationsAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            await context.Database.MigrateAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false"
                });
            });
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
