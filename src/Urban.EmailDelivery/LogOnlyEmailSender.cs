using Microsoft.Extensions.Logging;
using Urban.SharedKernel.Abstractions;

namespace Urban.EmailDelivery;

/// <summary>
/// Não envia: registra que um e-mail deixou de ser enviado, sem o corpo (ele carrega links com segredo) e sem o endereço
/// completo. Serve ao desenvolvimento e aos testes.
/// </summary>
public sealed partial class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogNotSent(logger, message.RecipientDomain, message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "E-mail não enviado (provedor Log). Destino em {Domain}. Assunto: {Subject}")]
    private static partial void LogNotSent(ILogger logger, string domain, string subject);
}