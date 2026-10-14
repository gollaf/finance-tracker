using System.Security.Claims;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Api.Common
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// The signed-in user's id. Identity stores it in the NameIdentifier
        /// claim of every token it issues. Only call this from endpoints that
        /// require sign-in: there, a missing or malformed claim is a bug, not
        /// bad input.
        /// </summary>
        public static UserId GetUserId(this ClaimsPrincipal user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) && id != Guid.Empty
                ? new UserId(id)
                : throw new InvalidOperationException("The signed-in user has no valid user id claim.");
        }
    }
}
