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
    /// Boots the real Api host -- the same Program.cs, the same DI wiring
    /// -- against a real, disposable Postgres container instead of
    /// whatever's in User Secrets, and applies migrations to it before any
    /// test runs. One instance is shared across every test in a class (via
    /// IClassFixture&lt;CustomWebApplicationFactory&gt;), not created fresh
    /// per test like FinanceTracker.Infrastructure.IntegrationTests does --
    /// faster, at the cost of tests within the same class sharing database
    /// state. Assert on the specific entities a test itself created, not on
    /// whole-table counts, to stay isolated from other tests in the class.
    ///
    /// The connection string is injected via an environment variable, not
    /// WebApplicationFactory's usual ConfigureWebHost/ConfigureAppConfiguration
    /// override. Program.cs calls AddInfrastructure(builder.Configuration)
    /// -- which reads the connection string immediately and throws if it's
    /// missing -- before builder.Build() runs. ConfigureWebHost's
    /// customizations only get applied during Build(), which for a
    /// minimal-hosting Program.cs like this one is too late: code between
    /// CreateBuilder(args) and Build() already ran against whatever
    /// configuration existed at that point. Environment variables, by
    /// contrast, are read as part of CreateBuilder(args) itself -- the very
    /// first line of Program.cs -- so setting one here, before Services is
    /// ever touched, guarantees Program.cs sees it in time. (Locally, User
    /// Secrets happens to also be read early enough to mask this; a CI
    /// runner with no User Secrets at all is what exposed it.)
    /// </summary>
    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            Environment.SetEnvironmentVariable("ConnectionStrings__FinanceTracker", _postgres.GetConnectionString());

            // Accessing Services triggers the host to actually build --
            // by now the environment variable above is already set, so
            // Program.cs's AddInfrastructure() call finds it.
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            await context.Database.MigrateAsync();
        }

        // Unlike the connection string above, this doesn't need the
        // environment-variable workaround -- it doesn't matter that
        // AddInfrastructure() already registered the real
        // GroqInsightsGenerator by the time this runs; ConfigureTestServices
        // is applied to the same IServiceCollection afterward, and a later
        // registration for the same interface simply replaces the earlier
        // one (TryAddSingleton wouldn't, which is why this uses the plain
        // Add call via RemoveAll + AddSingleton instead). See
        // StubInsightsGenerator's own doc comment for why every
        // Api.IntegrationTests test needs this instead of hitting Groq for
        // real.
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
