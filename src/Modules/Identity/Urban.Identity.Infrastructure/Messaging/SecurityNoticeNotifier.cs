using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta os avisos de segurança (senha trocada, troca de e-mail pedida ou concluída). Nenhum leva senha nem link de acesso:
/// só dizem o que aconteceu e mandam a pessoa à tela de recuperação se não foi ela. Se o provedor falhar, registra o erro
/// (sem os endereços) e relança para a tarefa em segundo plano tentar de novo.
/// </summary>
public sealed partial class SecurityNoticeNotifier(IEmailSender sender, WebLinksOptions links, ILogger<SecurityNoticeNotifier> logger)
    : ISecurityNoticeNotifier
{
    public async Task SendAsync(SecurityNotice notice, Email recipient, Email? other, CancellationToken cancellationToken)
    {
        string recovery = new Uri(links.GetWebBaseUri(), "esqueci-a-senha").AbsoluteUri;
        (string subject, string body) = notice switch
        {
            SecurityNotice.PasswordChanged => PasswordChanged(recovery),
            SecurityNotice.EmailChangeRequested => EmailChangeRequested(RequireOther(other)),
            SecurityNotice.EmailChanged => EmailChanged(RequireOther(other), recovery),
            _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, "Aviso de segurança desconhecido."),
        };

        EmailMessage message = new(recipient.Value, subject, body);
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

    private static Email RequireOther(Email? other) =>
        other ?? throw new ArgumentNullException(nameof(other), "Este aviso cita um segundo endereço.");

    private static (string Subject, string Body) PasswordChanged(string recovery) =>
        ("A senha da sua conta no Urban foi alterada", $"""
            Olá,

            A senha da sua conta no Urban acabou de ser alterada, e todos os aparelhos conectados foram desconectados.

            Se foi você, não precisa fazer nada. Se não foi, redefina a senha agora para recuperar o acesso:

            {recovery}

            Urban
            """);

    private static (string Subject, string Body) EmailChangeRequested(Email requested) =>
        ("Pedido para trocar o e-mail da sua conta no Urban", $"""
            Olá,

            Alguém, com a senha da sua conta, pediu para trocar o e-mail dela para {requested.Value}. Enviamos um link de confirmação para esse endereço.

            Até que o link seja aberto, este e-mail continua valendo e nada muda. Se o pedido não foi seu, troque sua senha assim que puder.

            Urban
            """);

    private static (string Subject, string Body) EmailChanged(Email current, string recovery) =>
        ("O e-mail da sua conta no Urban foi alterado", $"""
            Olá,

            O e-mail da sua conta no Urban foi trocado para {current.Value}. A partir de agora, a entrada é feita com o novo endereço, e os aparelhos conectados foram desconectados.

            Se não foi você, redefina a senha para recuperar o acesso:

            {recovery}

            Urban
            """);

    [LoggerMessage(Level = LogLevel.Error, Message = "O aviso de segurança não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
