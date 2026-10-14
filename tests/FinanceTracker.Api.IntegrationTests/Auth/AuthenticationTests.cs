using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Api.IntegrationTests.Auth
{
    /// <summary>
    /// Signing in through /api/auth, and what a request with and without a
    /// token is allowed to reach.
    /// </summary>
    public sealed class AuthenticationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthenticationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Any id works: these tests are about getting past sign-in, not about the account.
        private static string BalanceUrl => $"/api/accounts/{Guid.NewGuid()}/balance";

        [Fact]
        public async Task ApiEndpoint_WithoutToken_Returns401()
        {
            var client = _factory.CreateAnonymousClient();

            var response = await client.GetAsync(BalanceUrl);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ApiEndpoint_WithToken_GetsPastSignIn()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(BalanceUrl);

            // 404 from the handler, not 401: the request was authenticated.
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RegisterThenLogin_ReturnsBearerToken()
        {
            var client = _factory.CreateAnonymousClient();
            var credentials = new { email = $"{Guid.NewGuid():N}@example.com", password = "An0ther-Passw0rd!" };

            var register = await client.PostAsJsonAsync("/api/auth/register", credentials);
            var login = await client.PostAsJsonAsync("/api/auth/login", credentials);

            register.StatusCode.Should().Be(HttpStatusCode.OK);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
            token!.AccessToken.Should().NotBeNullOrWhiteSpace();
            token.RefreshToken.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Login_WithWrongPassword_Returns401()
        {
            var client = _factory.CreateAnonymousClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = CustomWebApplicationFactory.TestUserEmail,
                password = "Wrong-Passw0rd!",
            });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("/health/live")]
        [InlineData("/openapi/v1.json")]
        public async Task HealthAndApiDocs_WithoutToken_Return200(string path)
        {
            var client = _factory.CreateAnonymousClient();

            var response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SigningIn_StoresDataProtectionKeysInTheDatabase()
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();

            var keyCount = await dbContext.DataProtectionKeys.CountAsync();

            // The factory already signed its test user in, which created a key.
            keyCount.Should().BePositive();
        }

        [Fact]
        public async Task Token_FromOneInstance_IsAcceptedByAnother()
        {
            // A second host on the same database: a separate key ring in
            // memory, like a second container or the same one after a restart.
            var otherInstance = _factory.WithWebHostBuilder(_ => { });
            var client = otherInstance.CreateClient();

            var response = await client.GetAsync(BalanceUrl);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
