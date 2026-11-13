using System.Globalization;
using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Contact;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta e envia o e-mail da resposta do dono ao síndico, com o administrador em <c>Reply-To</c> (o remetente é um endereço "não
/// responda") e os arquivos anexados. O envio é na hora, dentro da requisição: se o provedor recusar, o administrador vê o erro e
/// tenta de novo, e nada fica registrado como enviado. No log aparece no máximo o domínio de quem recebe, nunca o texto nem os arquivos.
/// </summary>
public sealed partial class ContactReplyMailer(IEmailSender sender, ILogger<ContactReplyMailer> logger) : IContactReplyMailer
{
    private static readonly TimeSpan BrazilOffset = TimeSpan.FromHours(-3);

    public async Task<Result> SendAsync(ContactReplyEmail email, CancellationToken cancellationToken)
    {
        string greeting = email.RecipientName is null ? "Olá," : $"Olá {email.RecipientName},";
        string sentOn = email.OriginalSentAt.ToOffset(BrazilOffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string text = $"""
            {greeting}

            {email.Body}

            ---
            Em resposta à sua mensagem "{email.OriginalSubject}", enviada em {sentOn}.
            Você pode responder diretamente a este e-mail.

            Urban
            """;

        EmailMessage message = new(
            email.To,
            $"Re: {email.OriginalSubject}",
            text,
            email.ReplyTo,
            [.. email.Files.Select(file => new EmailAttachment(file.FileName, file.Content))]);

        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            LogNotDelivered(logger, ex, message.RecipientDomain);
            return Result.Failure(ContactErrors.ReplyNotDelivered);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A resposta a uma mensagem de contato não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
