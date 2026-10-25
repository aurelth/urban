using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta o e-mail com o link de confirmação (tela do site) e entrega ao <see cref="IEmailSender"/>. Se o provedor falhar, registra
/// o erro (um por tentativa, sem o endereço) e relança: quem decide tentar de novo é a tarefa em segundo plano.
/// </summary>
public sealed partial class EmailVerificationNotifier(IEmailSender sender, WebLinksOptions links, ILogger<EmailVerificationNotifier> logger)
    : IEmailVerificationNotifier
{
    private const string Subject = "Confirme seu e-mail no Urban";

    public async Task SendAsync(Email to, string token, CancellationToken cancellationToken)
    {
        string link = $"{new Uri(links.GetWebBaseUri(), "confirmar-email").AbsoluteUri}?token={Uri.EscapeDataString(token)}";
        int hours = (int)EmailVerificationToken.Lifetime.TotalHours;

        string body = $"""
            Olá,

            Recebemos um pedido de cadastro no Urban com este e-mail. Para confirmar e criar sua senha, abra o link abaixo. Ele vale por {hours} horas e só pode ser usado uma vez:

            {link}

            Se você não fez esse pedido, ignore esta mensagem.

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

    [LoggerMessage(Level = LogLevel.Error, Message = "O e-mail de confirmação não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}