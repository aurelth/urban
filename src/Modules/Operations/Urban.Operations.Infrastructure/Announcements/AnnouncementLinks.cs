using Urban.Operations.Application.Abstractions;

namespace Urban.Operations.Infrastructure.Announcements;

/// <summary>
/// Os endereços do site que aparecem nos e-mails dos comunicados, montados sobre o endereço público da configuração
/// (<c>Links:WebBaseUrl</c>). O endereço só é conferido quando um e-mail é montado, para o módulo registrar sem a seção.
/// </summary>
public sealed class AnnouncementLinks(string? webBaseUrl) : IAnnouncementLinks
{
    public Uri GetMuralUri()
    {
        bool valid = Uri.TryCreate(webBaseUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https";

        return valid
            ? new Uri(new Uri(uri!.AbsoluteUri.TrimEnd('/') + "/"), "mural")
            : throw new InvalidOperationException("Falta o endereço do site (Links:WebBaseUrl) ou ele não é uma URL http/https absoluta.");
    }
}
