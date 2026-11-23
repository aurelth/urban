using Urban.Operations.Application.Abstractions;

namespace Urban.Operations.Infrastructure.Announcements;

/// <summary>Os endereços do site que aparecem nos e-mails dos comunicados, montados sobre o endereço público da configuração (<c>Links:WebBaseUrl</c>).</summary>
public sealed class AnnouncementLinks(string? webBaseUrl) : IAnnouncementLinks
{
    public Uri GetMuralUri() =>
        throw new NotImplementedException($"Não implementado: {webBaseUrl}");
}
