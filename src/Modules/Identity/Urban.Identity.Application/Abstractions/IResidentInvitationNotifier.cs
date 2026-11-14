using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que entrega o convite ao morador. Texto, endereço do site e canal de envio são detalhes da Infrastructure.</summary>
public interface IResidentInvitationNotifier
{
    /// <param name="condominiumName">Nome do condomínio, para o morador reconhecer quem o convidou; nulo quando não foi possível descobrir.</param>
    Task SendAsync(Email to, string? condominiumName, string token, CancellationToken cancellationToken);
}
