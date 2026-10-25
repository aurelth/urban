using System.Net.Mail;

namespace Urban.EmailDelivery;

/// <summary>Configuração do envio de e-mail (seção "Email"). A chave do Brevo vem do ambiente, nunca do repositório.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Obrigatório: sem ele a API não sobe, para um ambiente mal configurado não ficar só registrando em log.</summary>
    public EmailProvider? Provider { get; init; }

    public string SenderAddress { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public BrevoOptions Brevo { get; init; } = new();

    /// <exception cref="InvalidOperationException">A configuração está incompleta. A mensagem nunca repete valores, para não vazar a chave.</exception>
    public void Validate()
    {
        if (Provider is null)
        {
            throw new InvalidOperationException($"Falta o provedor de e-mail ({SectionName}:{nameof(Provider)}). Use Brevo ou Log.");
        }

        if (!MailAddress.TryCreate(SenderAddress, out MailAddress? sender) || sender.Address != SenderAddress)
        {
            throw new InvalidOperationException($"Falta o endereço do remetente ({SectionName}:{nameof(SenderAddress)}) ou ele não é um e-mail válido.");
        }

        if (string.IsNullOrWhiteSpace(SenderName))
        {
            throw new InvalidOperationException($"Falta o nome do remetente ({SectionName}:{nameof(SenderName)}).");
        }

        if (Provider == EmailProvider.Brevo)
        {
            ValidateBrevo();
        }
    }

    private void ValidateBrevo()
    {
        if (string.IsNullOrWhiteSpace(Brevo.ApiKey))
        {
            throw new InvalidOperationException($"Falta a chave do Brevo ({SectionName}:{nameof(Brevo)}:{nameof(BrevoOptions.ApiKey)}).");
        }

        bool https = Uri.TryCreate(Brevo.BaseUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
        if (!https)
        {
            throw new InvalidOperationException(
                $"O endereço da API do Brevo ({SectionName}:{nameof(Brevo)}:{nameof(BrevoOptions.BaseUrl)}) precisa ser uma URL https absoluta.");
        }
    }
}