using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.RateLimiting
{
    /// <summary>
    /// Limits how many requests one client address may make: every endpoint
    /// counts against a general limit, and /api/auth also against a stricter
    /// one. A rejected request gets 429 with a Retry-After header.
    /// </summary>
    public static class RateLimitingSetup
    {
        public const string AuthPolicy = "auth";

        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));

            services.AddRateLimiter(options =>
            {
                // The default, 503, would claim the server is failing.
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = WriteRejectionAsync;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var settings = Settings(httpContext);
                    return RateLimitPartition.GetFixedWindowLimiter(ClientAddress(httpContext), _ =>
                        new FixedWindowRateLimiterOptions { PermitLimit = settings.PermitLimit, Window = settings.Window });
                });

                options.AddPolicy(AuthPolicy, httpContext =>
                {
                    var settings = Settings(httpContext);
                    return RateLimitPartition.GetFixedWindowLimiter(ClientAddress(httpContext), _ =>
                        new FixedWindowRateLimiterOptions { PermitLimit = settings.AuthPermitLimit, Window = settings.AuthWindow });
                });
            });

            return services;
        }

        // Read per request rather than captured here: test hosts add their
        // configuration only when the app is built.
        private static RateLimitingSettings Settings(HttpContext httpContext) =>
            httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingSettings>>().Value;

        // The forwarded-headers middleware runs first, so behind the proxy
        // this is the visitor's address, not the proxy's.
        private static string ClientAddress(HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                httpContext.Response.Headers.RetryAfter =
                    ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Too many requests from this address. Try again after the time in the Retry-After header.",
                },
            });
        }
    }
}
