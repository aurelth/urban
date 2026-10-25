namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Endereços do site que aparecem nos e-mails (seção "Links"). Mudam por ambiente, então vêm da configuração.</summary>
public sealed class WebLinksOptions
{
    public const string SectionName = "Links";

    /// <summary>Endereço público do site, por exemplo <c>https://urban-pro.io</c>.</summary>
    public string WebBaseUrl { get; init; } = string.Empty;

    /// <returns>O endereço com barra no final, para montar caminhos relativos a ele.</returns>
    public Uri GetWebBaseUri()
    {
        bool valid = Uri.TryCreate(WebBaseUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https";

        return valid
            ? new Uri(uri!.AbsoluteUri.TrimEnd('/') + "/")
            : throw new InvalidOperationException(
                $"Falta o endereço do site ({SectionName}:{nameof(WebBaseUrl)}) ou ele não é uma URL http/https absoluta.");
    }
}