using Microsoft.AspNetCore.SignalR;
using Urban.Api.Http;
using Urban.Operations.Application.Abstractions;

namespace Urban.Api.Hubs;

public static class RealtimeExtensions
{
    /// <summary>Raiz de todos os hubs; é a única em que o token de acesso pode vir na URL (ver a autenticação).</summary>
    public const string HubsPrefix = "/hubs";

    public static IServiceCollection AddUrbanRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddSingleton<IAnnouncementEvents, SignalRAnnouncementEvents>();

        return services;
    }

    public static WebApplication MapUrbanHubs(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHub<AnnouncementsHub>(AnnouncementsHub.Path).RequireRateLimiting(RateLimitPolicies.Announcements);

        return app;
    }
}
