using Microsoft.Extensions.Logging;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Monta o e-mail do convite, com o link da tela em que o morador escolhe a senha (a mesma da confirmação de e-mail), e o entrega
/// ao <see cref="IEmailSender"/>. Se o provedor falhar, registra o erro (um por tentativa, sem o endereço) e relança: quem decide
/// tentar de novo é a tarefa em segundo plano.
/// </summary>
public sealed partial class ResidentInvitationNotifier(IEmailSender sender, WebLinksOptions links, ILogger<ResidentInvitationNotifier> logger)
    : IResidentInvitationNotifier
{
    private const string Subject = "Você foi convidado para o Urban";

    public async Task SendAsync(Email to, string? condominiumName, string token, CancellationToken cancellationToken)
    {
        string link = $"{new Uri(links.GetWebBaseUri(), "confirmar-email").AbsoluteUri}?token={Uri.EscapeDataString(token)}";
        int hours = (int)EmailVerificationToken.InvitationLifetime.TotalHours;
        string who = string.IsNullOrWhiteSpace(condominiumName) ? "do seu condomínio" : $"do condomínio {condominiumName}";

        string body = $"""
            Olá,

            O síndico {who} cadastrou você no Urban, o sistema de gestão do condomínio. Para ativar seu acesso e criar sua senha, abra o link abaixo. Ele vale por {hours} horas e só pode ser usado uma vez:

            {link}

            Se você não esperava este convite, ignore esta mensagem.

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

    [LoggerMessage(Level = LogLevel.Error, Message = "O convite do morador não foi entregue ao provedor. Destino em {Domain}")]
    private static partial void LogNotDelivered(ILogger logger, Exception exception, string domain);
}
