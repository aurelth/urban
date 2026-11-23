using Microsoft.AspNetCore.SignalR;
using Urban.Api.Http;

namespace Urban.Api.Hubs;

/// <summary>Diz ao SignalR quem é a conta de cada conexão: o id do token de acesso, para o aviso ir à conta e não a uma conexão.</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return connection.User is { } principal && AccessTokenSession.TryRead(principal, out AccessTokenSession? session)
            ? session.UserId.ToString()
            : null;
    }
}
