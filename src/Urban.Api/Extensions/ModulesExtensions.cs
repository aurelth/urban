using Urban.Api.Http;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Infrastructure;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Extensions;

public static class ModulesExtensions
{
    public static IServiceCollection AddUrbanModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(options => options.RegisterServicesFromAssembly(Urban.Identity.Application.AssemblyReference.Assembly));
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddIdentityInfrastructure(configuration);
        services.AddCondominiumInfrastructure(configuration);

        return services;
    }

    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await services.MigrateIdentityAsync(cancellationToken);
        await services.MigrateCondominiumAsync(cancellationToken);
    }
}