using Microsoft.AspNetCore.SignalR;
using Urban.Operations.Application.Abstractions;

namespace Urban.Api.Hubs;

public static class RealtimeExtensions
{
    public static IServiceCollection AddUrbanRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddSingleton<IAnnouncementEvents, SignalRAnnouncementEvents>();

        return services;
    }

    public static WebApplication MapUrbanHubs(this WebApplication app) => app;
}
