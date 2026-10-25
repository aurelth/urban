using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.SharedKernel.Abstractions;

namespace Urban.EmailDelivery;

public static class EmailDeliveryExtensions
{
    private const int MaxResponseBytes = 64 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddEmailDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        EmailOptions options = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        options.Validate();
        services.AddSingleton(options);

        if (options.Provider == EmailProvider.Brevo)
        {
            services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
            {
                client.BaseAddress = new Uri(options.Brevo.BaseUrl);
                client.Timeout = RequestTimeout;
                client.MaxResponseContentBufferSize = MaxResponseBytes;
            });
        }
        else
        {
            services.AddSingleton<IEmailSender, LogOnlyEmailSender>();
        }

        return services;
    }
}