using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta o e-mail com o link de redefinição (tela do site) e entrega ao <see cref="IEmailSender"/>. Se o provedor falhar, registra
/// o erro (um por tentativa, sem o endereço) e relança: quem decide tentar de novo é a tarefa em segundo plano.
/// </summary>
public sealed partial class PasswordResetNotifier(IEmailSender sender, WebLinksOptions links, ILogger<PasswordResetNotifier> logger)
    : IPasswordResetNotifier
{
    private const string Subject = "Redefina sua senha no Urban";

    public async Task SendAsync(Email to, string token, CancellationToken cancellationToken)
    {
        string link = $"{new Uri(links.GetWebBaseUri(), "redefinir-senha").AbsoluteUri}?token={Uri.EscapeDataString(token)}";
        int minutes = (int)PasswordResetToken.Lifetime.TotalMinutes;

        string body = $"""
            Olá,

            Recebemos um pedido para redefinir a senha da sua conta no Urban. Para escolher uma nova senha, abra o link abaixo. Ele vale por {minutes} minutos e só pode ser usado uma vez:

            {link}

            Se você não fez esse pedido, ignore esta mensagem: sua senha continua a mesma.

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

    [LoggerMessage(Level = LogLevel.Error, Message = "O e-mail de redefinição de senha não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}