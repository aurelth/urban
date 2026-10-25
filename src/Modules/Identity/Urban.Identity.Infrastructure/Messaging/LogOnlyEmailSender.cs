using Microsoft.Extensions.Logging;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Remetente provisório: registra que um e-mail deixou de ser enviado, sem o corpo (ele carrega o link de confirmação) e
/// sem o endereço completo. Fica até existir um provedor real configurado.
/// </summary>
public sealed partial class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogNotSent(logger, DomainOf(message.To), message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "E-mail não enviado (nenhum provedor configurado). Destino em {Domain}. Assunto: {Subject}")]
    private static partial void LogNotSent(ILogger logger, string domain, string subject);

    private static string DomainOf(string address) => address[(address.IndexOf('@') + 1)..];
}