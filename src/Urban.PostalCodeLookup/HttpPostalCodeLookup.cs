using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>
/// O que BrasilAPI e ViaCEP têm em comum: uma requisição GET, qualquer falha virando "indisponível" (com o motivo no log, nunca
/// o CEP) e a leitura dos quatro campos do endereço. Cada provedor só diz o caminho, os nomes dos campos e como avisa "não existe".
/// </summary>
public abstract partial class HttpPostalCodeLookup(HttpClient http, ILogger logger, string provider) : IPostalCodeLookup
{
    public async Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken)
    {
        PostalCodeFormat.EnsureValid(postalCode);

        using HttpRequestMessage request = new(HttpMethod.Get, RelativePath(postalCode));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            return await ReadAsync(response, postalCode, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(logger, provider, ex);
            return PostalCodeLookupResult.Unavailable;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(logger, provider, ex);
            return PostalCodeLookupResult.Unavailable;
        }
    }

    protected abstract string RelativePath(string postalCode);

    protected abstract Task<PostalCodeLookupResult> ReadAsync(HttpResponseMessage response, string postalCode, CancellationToken cancellationToken);

    protected PostalCodeLookupResult Refused(HttpStatusCode status)
    {
        LogRefused(logger, provider, (int)status);
        return PostalCodeLookupResult.Unavailable;
    }

    protected PostalCodeLookupResult Unreadable()
    {
        LogUnreadable(logger, provider);
        return PostalCodeLookupResult.Unavailable;
    }

    /// <summary>Lê o corpo como um objeto JSON; devolve nulo se não for JSON ou se for outra coisa (lista, texto, número).</summary>
    protected static async Task<JsonElement?> ReadObjectAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Monta o endereço com o CEP que foi pedido (nunca o que o provedor devolveu). Cidade e UF são obrigatórias; rua e bairro
    /// podem faltar nos CEPs gerais de cidades pequenas e viram texto vazio. Uma UF que não é a dona da faixa do CEP mostra que o
    /// provedor errou: a resposta vira "não existe" (e o reserva é consultado), em vez de preencher um endereço absurdo.
    /// </summary>
    protected PostalCodeLookupResult ToAddress(JsonElement body, string postalCode, string street, string neighborhood, string city, string state)
    {
        string? cityName = Text(body, city);
        string? stateName = Text(body, state);
        if (string.IsNullOrEmpty(cityName) || string.IsNullOrEmpty(stateName))
        {
            return Unreadable();
        }

        if (!PostalCodeStateRanges.Matches(postalCode, stateName))
        {
            LogStateOutsideRange(logger, provider);
            return PostalCodeLookupResult.NotFound;
        }

        return PostalCodeLookupResult.Found(new PostalCodeAddress(
            postalCode,
            Text(body, street) ?? string.Empty,
            Text(body, neighborhood) ?? string.Empty,
            cityName,
            stateName));
    }

    protected static string? Text(JsonElement body, string field) =>
        body.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "A consulta de CEP na {Provider} respondeu HTTP {Status}")]
    private static partial void LogRefused(ILogger logger, string provider, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A consulta de CEP na {Provider} respondeu com conteúdo ilegível")]
    private static partial void LogUnreadable(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Provider} devolveu uma UF que não corresponde à faixa do CEP consultado")]
    private static partial void LogStateOutsideRange(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível falar com a {Provider} na consulta de CEP")]
    private static partial void LogNetworkFailure(ILogger logger, string provider, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Provider} não respondeu a tempo na consulta de CEP")]
    private static partial void LogTimeout(ILogger logger, string provider, Exception exception);
}
