using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>
/// Provedor principal. Responde 404 quando o CEP não existe, mas também quando os serviços por trás dela falham; por isso o
/// "não existe" dela ainda passa pela confirmação do provedor de reserva (<see cref="FallbackPostalCodeLookup"/>).
/// </summary>
public sealed class BrasilApiPostalCodeLookup(HttpClient http, ILogger<BrasilApiPostalCodeLookup> logger)
    : HttpPostalCodeLookup(http, logger, "BrasilAPI")
{
    protected override string RelativePath(string postalCode) => $"api/cep/v1/{postalCode}";

    protected override async Task<PostalCodeLookupResult> ReadAsync(HttpResponseMessage response, string postalCode, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return PostalCodeLookupResult.NotFound;
        }

        if (!response.IsSuccessStatusCode)
        {
            return Refused(response.StatusCode);
        }

        JsonElement? body = await ReadObjectAsync(response, cancellationToken);

        return body is null ? Unreadable() : ToAddress(body.Value, postalCode, "street", "neighborhood", "city", "state");
    }
}
