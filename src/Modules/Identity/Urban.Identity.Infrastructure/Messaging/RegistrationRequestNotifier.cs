using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Avisa o dono por e-mail de um pedido de cadastro novo, com o link da tela onde ele decide. Os dados do pedido só vão no corpo
/// da mensagem; nos registros de log aparece no máximo o domínio do destino. Se o provedor falhar, registra o erro e relança:
/// quem decide tentar de novo é a tarefa em segundo plano.
/// </summary>
public sealed partial class RegistrationRequestNotifier(
    IEmailSender sender,
    WebLinksOptions links,
    SuperAdminOptions owner,
    ILogger<RegistrationRequestNotifier> logger) : IRegistrationRequestNotifier
{
    private const string Subject = "Novo pedido de cadastro no Urban";

    public async Task NotifyAsync(RegistrationRequestSummary request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner.Email))
        {
            LogNoOwner(logger);
            return;
        }

        string link = new Uri(links.GetWebBaseUri(), "admin/pedidos-de-cadastro").AbsoluteUri;
        string body = $"""
            Olá,

            Há um novo pedido de cadastro de síndico aguardando sua decisão:

            Nome: {request.Name}
            E-mail: {request.Email}
            Condomínio: {request.CondominiumName}

            Para aprovar ou recusar, abra:
            {link}

            Urban
            """;

        EmailMessage message = new(owner.Email.Trim(), Subject, body);

        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            LogNotDelivered(logger, ex, message.RecipientDomain);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Há um pedido de cadastro novo, mas o e-mail do dono (SuperAdmin:Email) não está configurado. O pedido está na lista de pedidos")]
    private static partial void LogNoOwner(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "O aviso de pedido de cadastro não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
