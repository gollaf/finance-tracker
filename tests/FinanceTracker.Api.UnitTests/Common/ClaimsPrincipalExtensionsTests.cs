using System.Security.Claims;
using FinanceTracker.Api.Common;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Api.UnitTests.Common
{
    public sealed class ClaimsPrincipalExtensionsTests
    {
        private static ClaimsPrincipal UserWith(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, authenticationType: "Test"));

        [Fact]
        public void GetUserId_WithNameIdentifierClaim_ReturnsIt()
        {
            var id = Guid.NewGuid();

            var userId = UserWith(new Claim(ClaimTypes.NameIdentifier, id.ToString())).GetUserId();

            userId.Should().Be(new UserId(id));
        }

        [Fact]
        public void GetUserId_WithoutTheClaim_Throws()
        {
            var act = () => UserWith(new Claim(ClaimTypes.Email, "someone@example.com")).GetUserId();

            act.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void GetUserId_WithMalformedOrEmptyId_Throws(string value)
        {
            var act = () => UserWith(new Claim(ClaimTypes.NameIdentifier, value)).GetUserId();

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
