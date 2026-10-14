using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinanceTracker.Api.OpenApi
{
    /// <summary>
    /// Declares the sign-in token in the OpenAPI document, so the API docs can
    /// send it with every request. The sign-in endpoints themselves don't get
    /// the requirement: they are how a caller gets a token in the first place.
    /// </summary>
    internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(
            OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                [SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    Description = "The accessToken returned by POST /api/auth/login.",
                },
            };

            var protectedOperations = document.Paths
                .Where(path => !IsSignInPath(path.Key) && path.Value.Operations is not null)
                .SelectMany(path => path.Value.Operations!);

            foreach (var operation in protectedOperations)
            {
                operation.Value.Security ??= [];
                operation.Value.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
                });
            }

            return Task.CompletedTask;
        }

        // Under /api/auth only /manage/* needs a signed-in user.
        private static bool IsSignInPath(string path) =>
            path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/auth/manage/", StringComparison.OrdinalIgnoreCase);
    }
}
