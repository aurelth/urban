namespace Urban.PostalCodeLookup;

/// <summary>Configuração da consulta de CEP (seção "PostalCodeLookup"). Sem os endereços dos dois provedores, a API não sobe.</summary>
public sealed class PostalCodeLookupOptions
{
    public const string SectionName = "PostalCodeLookup";

    public PostalCodeProviderOptions BrasilApi { get; init; } = new();

    public PostalCodeProviderOptions ViaCep { get; init; } = new();

    /// <exception cref="InvalidOperationException">A configuração é inválida. A mensagem nunca repete o valor recusado.</exception>
    public void Validate()
    {
        Validate(nameof(BrasilApi), BrasilApi);
        Validate(nameof(ViaCep), ViaCep);
    }

    private static void Validate(string provider, PostalCodeProviderOptions options)
    {
        bool https = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
        if (!https)
        {
            throw new InvalidOperationException(
                $"O endereço da consulta de CEP ({SectionName}:{provider}:{nameof(PostalCodeProviderOptions.BaseUrl)}) precisa ser uma URL https absoluta.");
        }
    }
}

public sealed class PostalCodeProviderOptions
{
    /// <summary>Termina com "/": o caminho de cada consulta é somado a ela.</summary>
    public string BaseUrl { get; init; } = string.Empty;
}
