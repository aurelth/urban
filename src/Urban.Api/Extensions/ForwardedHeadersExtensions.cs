using Microsoft.AspNetCore.HttpOverrides;
using Urban.Api.Http;

namespace Urban.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddUrbanForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        TrustedProxiesOptions proxies = configuration.GetSection(TrustedProxiesOptions.SectionName).Get<TrustedProxiesOptions>() ?? new TrustedProxiesOptions();
        proxies.Validate();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = proxies.Hops;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (System.Net.IPNetwork network in proxies.ParseNetworks())
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }
}