using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Urban.IntegrationTests.Api;

/// <summary>O servidor de teste não tem soquete, então não há endereço remoto. Este filtro faz o papel do proxy que abriria a conexão.</summary>
internal sealed class RemoteAddressStartupFilter(IPAddress address) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = address;
            return nextMiddleware(context);
        });

        next(app);
    };
}