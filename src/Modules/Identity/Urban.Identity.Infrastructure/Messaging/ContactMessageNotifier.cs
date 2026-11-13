using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Contact;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Avisa o dono por e-mail de uma mensagem de contato nova, com o texto e o link da caixa de entrada. Os dados da mensagem só vão
/// no corpo do e-mail; nos registros de log aparece no máximo o domínio do destino. Se o provedor falhar, registra o erro e
/// relança: quem decide tentar de novo é a tarefa em segundo plano.
/// </summary>
public sealed partial class ContactMessageNotifier(
    IEmailSender sender,
    WebLinksOptions links,
    SuperAdminOptions owner,
    ILogger<ContactMessageNotifier> logger) : IContactMessageNotifier
{
    private const string Subject = "Nova mensagem de contato no Urban";

    public async Task NotifyAsync(ContactMessageSummary message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner.Email))
        {
            LogNoOwner(logger);
            return;
        }

        string link = new Uri(links.GetWebBaseUri(), "admin/mensagens").AbsoluteUri;
        string body = $"""
            Olá,

            Um síndico enviou uma mensagem pelo Urban:

            Nome: {message.SenderName ?? message.SenderEmail}
            E-mail: {message.SenderEmail}
            Condomínio: {message.CondominiumName ?? "ainda não cadastrado"}

            Assunto: {message.Subject}

            {message.Body}

            Para ver todas as mensagens, abra:
            {link}

            Urban
            """;

        EmailMessage email = new(owner.Email.Trim(), Subject, body);

        try
        {
            await sender.SendAsync(email, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            LogNotDelivered(logger, ex, email.RecipientDomain);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Há uma mensagem de contato nova, mas o e-mail do dono (SuperAdmin:Email) não está configurado. A mensagem está na caixa de entrada")]
    private static partial void LogNoOwner(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "O aviso de mensagem de contato não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
