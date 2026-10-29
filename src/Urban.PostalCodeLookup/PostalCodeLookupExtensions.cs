using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

public static class PostalCodeLookupExtensions
{
    private const int MaxResponseBytes = 64 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Registra a consulta de CEP: cache na frente, BrasilAPI como principal e ViaCEP como reserva. Exige um <see cref="IClock"/> registrado.</summary>
    public static IServiceCollection AddPostalCodeLookup(this IServiceCollection services, IConfiguration configuration)
    {
        PostalCodeLookupOptions options = configuration.GetSection(PostalCodeLookupOptions.SectionName).Get<PostalCodeLookupOptions>()
            ?? new PostalCodeLookupOptions();
        options.Validate();
        services.AddSingleton(options);

        services.AddHttpClient<BrasilApiPostalCodeLookup>(client => Configure(client, options.BrasilApi));
        services.AddHttpClient<ViaCepPostalCodeLookup>(client => Configure(client, options.ViaCep));

        services.AddSingleton(provider => new PostalCodeCache(provider.GetRequiredService<IClock>()));
        services.AddTransient<IPostalCodeLookup>(provider => new CachedPostalCodeLookup(
            new FallbackPostalCodeLookup(
                provider.GetRequiredService<BrasilApiPostalCodeLookup>(),
                provider.GetRequiredService<ViaCepPostalCodeLookup>()),
            provider.GetRequiredService<PostalCodeCache>()));

        return services;
    }

    private static void Configure(HttpClient client, PostalCodeProviderOptions provider)
    {
        client.BaseAddress = new Uri(provider.BaseUrl);
        client.Timeout = RequestTimeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;
    }
}
