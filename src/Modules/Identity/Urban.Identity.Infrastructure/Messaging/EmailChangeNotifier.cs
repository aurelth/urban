using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta o e-mail com o link de confirmação (tela do site) e entrega ao <see cref="IEmailSender"/>, no endereço NOVO. Se o
/// provedor falhar, registra o erro (um por tentativa, sem o endereço nem o link) e relança: quem decide tentar de novo é a
/// tarefa em segundo plano.
/// </summary>
public sealed partial class EmailChangeNotifier(IEmailSender sender, WebLinksOptions links, ILogger<EmailChangeNotifier> logger)
    : IEmailChangeNotifier
{
    private const string Subject = "Confirme o novo e-mail da sua conta no Urban";

    public async Task SendLinkAsync(Email to, string token, CancellationToken cancellationToken)
    {
        string link = $"{new Uri(links.GetWebBaseUri(), "confirmar-troca-de-email").AbsoluteUri}?token={Uri.EscapeDataString(token)}";
        int hours = (int)EmailChangeToken.Lifetime.TotalHours;

        string body = $"""
            Olá,

            Recebemos um pedido para usar este endereço como o e-mail da sua conta no Urban. Para confirmar a troca, abra o link abaixo. Ele vale por {hours} horas e só pode ser usado uma vez:

            {link}

            Se você não fez esse pedido, ignore esta mensagem: nada muda na conta.

            Urban
            """;

        EmailMessage message = new(to.Value, Subject, body);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "O e-mail de confirmação da troca de e-mail não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
