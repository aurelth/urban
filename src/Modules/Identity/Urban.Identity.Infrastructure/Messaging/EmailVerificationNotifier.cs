using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Monta o e-mail com o link de confirmação (tela do site) e entrega ao <see cref="IEmailSender"/>.</summary>
public sealed class EmailVerificationNotifier(IEmailSender sender, WebLinksOptions links) : IEmailVerificationNotifier
{
    private const string Subject = "Confirme seu e-mail no Urban";

    public Task SendAsync(Email to, string token, CancellationToken cancellationToken)
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

        return sender.SendAsync(new EmailMessage(to.Value, Subject, body), cancellationToken);
    }
}