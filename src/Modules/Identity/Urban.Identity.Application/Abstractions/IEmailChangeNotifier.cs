using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que entrega o link de troca de e-mail ao endereço NOVO.</summary>
public interface IEmailChangeNotifier
{
    Task SendLinkAsync(Email to, string token, CancellationToken cancellationToken);
}
