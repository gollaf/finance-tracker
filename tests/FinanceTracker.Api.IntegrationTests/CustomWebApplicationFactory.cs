using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Api.IntegrationTests
{
    /// <summary>
    /// Boots the real Api host against a disposable Postgres container and
    /// migrates it. One instance is shared by every test in a class, so
    /// tests assert on the entities they created, not on table counts.
    ///
    /// The connection string goes in through an environment variable, not
    /// ConfigureAppConfiguration: Program.cs reads it in AddInfrastructure,
    /// before Build(), and WebApplicationFactory's configuration overrides
    /// are only applied during Build(). Environment variables are already
    /// read by CreateBuilder.
    /// </summary>
    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            Environment.SetEnvironmentVariable("ConnectionStrings__FinanceTracker", _postgres.GetConnectionString());

            // Accessing Services builds the host, after the variable is set.
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            await context.Database.MigrateAsync();
        }

        // Replaces the real Groq client with StubInsightsGenerator.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInsightsGenerator>();
                services.AddSingleton<IInsightsGenerator, StubInsightsGenerator>();
            });
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
