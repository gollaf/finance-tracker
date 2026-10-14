using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
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
    ///
    /// Every client it creates is signed in as one test user, through the
    /// real /api/auth endpoints; CreateAnonymousClient is the exception.
    /// </summary>
    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string TestUserEmail = "test-user@example.com";
        public const string TestUserPassword = "Test-Passw0rd!";

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        private string? _accessToken;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            Environment.SetEnvironmentVariable("ConnectionStrings__FinanceTracker", _postgres.GetConnectionString());

            // Accessing Services builds the host, after the variable is set.
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            await context.Database.MigrateAsync();

            // No token yet, so this client is anonymous (see ConfigureClient).
            using var client = CreateClient();
            _accessToken = await RegisterAndLogInAsync(client, TestUserEmail, TestUserPassword);
        }

        public HttpClient CreateAnonymousClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = null;
            return client;
        }

        /// <summary>A client signed in as a brand-new user, for checks across users.</summary>
        public async Task<HttpClient> CreateClientForNewUserAsync()
        {
            var client = CreateAnonymousClient();
            var token = await RegisterAndLogInAsync(client, $"{Guid.NewGuid():N}@example.com", TestUserPassword);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>Registers a user through /api/auth and returns their access token.</summary>
        public static async Task<string> RegisterAndLogInAsync(HttpClient client, string email, string password)
        {
            var credentials = new { email, password };

            var register = await client.PostAsJsonAsync("/api/auth/register", credentials);
            register.EnsureSuccessStatusCode();

            var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
            login.EnsureSuccessStatusCode();

            var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
            return token!.AccessToken;
        }

        // Runs for every client this factory creates, including those of
        // factories derived with WithWebHostBuilder.
        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);

            if (_accessToken is not null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
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
