using Microsoft.AspNetCore.HttpOverrides;

namespace FinanceTracker.Api.ReverseProxy
{
    /// <summary>
    /// Behind a reverse proxy every request arrives from the proxy's address;
    /// the visitor's address comes in the X-Forwarded-For header instead.
    /// Anyone can send that header, so it is believed only when the request
    /// comes from a network listed in ReverseProxy:TrustedNetworks. A proxy
    /// that uses headers of its own names them in
    /// ReverseProxy:ForwardedForHeader and ReverseProxy:ForwardedProtoHeader.
    /// </summary>
    public static class ForwardedHeadersSetup
    {
        public const string TrustedNetworksKey = "ReverseProxy:TrustedNetworks";
        public const string ForwardedForHeaderKey = "ReverseProxy:ForwardedForHeader";
        public const string ForwardedProtoHeaderKey = "ReverseProxy:ForwardedProtoHeader";

        public static IServiceCollection AddReverseProxySupport(this IServiceCollection services)
        {
            services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    var networks = configuration.GetSection(TrustedNetworksKey).Get<string[]>() ?? [];

                    // Both lists start out holding loopback only.
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();

                    // With both lists empty the middleware skips the check and
                    // trusts every sender, so no trusted network means no
                    // forwarded headers at all.
                    if (networks.Length == 0)
                    {
                        options.ForwardedHeaders = ForwardedHeaders.None;
                        return;
                    }

                    foreach (var network in networks)
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));

                    // Proto: generated links, like the API docs' server URL, say https.
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    // CloudFront, for one, sends the visitor's address and
                    // port in CloudFront-Viewer-Address, and its scheme in
                    // CloudFront-Forwarded-Proto: a load balancer behind it
                    // would replace X-Forwarded-Proto with its own scheme.
                    var forwardedForHeader = configuration[ForwardedForHeaderKey];
                    if (!string.IsNullOrWhiteSpace(forwardedForHeader))
                        options.ForwardedForHeaderName = forwardedForHeader;

                    var forwardedProtoHeader = configuration[ForwardedProtoHeaderKey];
                    if (!string.IsNullOrWhiteSpace(forwardedProtoHeader))
                        options.ForwardedProtoHeaderName = forwardedProtoHeader;
                })
                // A malformed network fails the start, not the first request.
                .ValidateOnStart();

            return services;
        }
    }
}
