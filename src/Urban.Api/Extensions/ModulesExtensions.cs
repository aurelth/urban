using Urban.Identity.Infrastructure;

namespace Urban.Api.Extensions;

public static class ModulesExtensions
{
    public static IServiceCollection AddUrbanModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(options => options.RegisterServicesFromAssembly(Urban.Identity.Application.AssemblyReference.Assembly));
        services.AddIdentityInfrastructure(configuration);

        return services;
    }

    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await services.MigrateIdentityAsync(cancellationToken);
    }
}