using System.Text.Json;
using Microsoft.Extensions.Logging;
using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>Provedor de reserva. Avisa "não existe" com HTTP 200 e o campo "erro" (verdadeiro ou o texto "true", conforme a versão).</summary>
public sealed class ViaCepPostalCodeLookup(HttpClient http, ILogger<ViaCepPostalCodeLookup> logger)
    : HttpPostalCodeLookup(http, logger, "ViaCEP")
{
    protected override string RelativePath(string postalCode) => $"ws/{postalCode}/json/";

    protected override async Task<PostalCodeLookupResult> ReadAsync(HttpResponseMessage response, string postalCode, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return Refused(response.StatusCode);
        }

        JsonElement? body = await ReadObjectAsync(response, cancellationToken);
        if (body is null)
        {
            return Unreadable();
        }

        return IsErrorFlag(body.Value)
            ? PostalCodeLookupResult.NotFound
            : ToAddress(body.Value, postalCode, "logradouro", "bairro", "localidade", "uf");
    }

    private static bool IsErrorFlag(JsonElement body) =>
        body.TryGetProperty("erro", out JsonElement flag)
        && (flag.ValueKind == JsonValueKind.True || (flag.ValueKind == JsonValueKind.String && flag.GetString() == "true"));
}
