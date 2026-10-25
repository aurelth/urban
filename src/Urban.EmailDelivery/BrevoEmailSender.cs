using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urban.SharedKernel.Abstractions;

namespace Urban.EmailDelivery;

/// <summary>Envia e-mail transacional pela API do Brevo. Toda falha vira <see cref="EmailDeliveryException"/>.</summary>
public sealed class BrevoEmailSender(HttpClient http, EmailOptions options) : IEmailSender
{
    private const string SendPath = "v3/smtp/email";
    private const string ApiKeyHeader = "api-key";
    private const int MaxErrorCodeLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        BrevoSendRequest body = new(
            new BrevoContact(options.SenderName, options.SenderAddress),
            [new BrevoContact(null, message.To)],
            message.Subject,
            message.TextBody);

        using HttpRequestMessage request = new(HttpMethod.Post, SendPath) { Content = JsonContent.Create(body, options: JsonOptions) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add(ApiKeyHeader, options.Brevo.ApiKey);

        using HttpResponseMessage response = await PostAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string? code = await ReadErrorCodeAsync(response, cancellationToken);
            throw new EmailDeliveryException($"O Brevo recusou o envio (HTTP {(int)response.StatusCode}, código {code ?? "desconhecido"}).");
        }
    }

    private async Task<HttpResponseMessage> PostAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailDeliveryException("Não foi possível falar com o Brevo.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException("O Brevo não respondeu a tempo.", ex);
        }
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("code", out JsonElement code)
                || code.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string value = code.GetString()!;
            return value.Length > MaxErrorCodeLength ? value[..MaxErrorCodeLength] : value;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException)
        {
            return null;
        }
    }
}