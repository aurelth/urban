using Urban.Api.Http;
using Urban.Api.Persistence;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Infrastructure;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Extensions;

public static class ModulesExtensions
{
    public static IServiceCollection AddUrbanModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(options => options.RegisterServicesFromAssemblies(
            Urban.Identity.Application.AssemblyReference.Assembly,
            Urban.Condominium.Application.AssemblyReference.Assembly));
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddIdentityInfrastructure(configuration);
        services.AddCondominiumInfrastructure(configuration);
        services.AddScoped<IAtomicWork, AtomicWork>();

        return services;
    }

    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await services.MigrateIdentityAsync(cancellationToken);
        await services.MigrateCondominiumAsync(cancellationToken);
    }
}