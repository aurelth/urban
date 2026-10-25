using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que entrega o link de confirmação à pessoa. Texto, endereço do site e canal de envio são detalhes da Infrastructure.</summary>
public interface IEmailVerificationNotifier
{
    Task SendAsync(Email to, string token, CancellationToken cancellationToken);
}