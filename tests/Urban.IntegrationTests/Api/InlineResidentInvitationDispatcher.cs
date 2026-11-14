using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Invitations.SendResidentInvitation;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o envio do convite na hora, dentro da requisição, para os testes de API lerem o e-mail logo depois do pedido, sem esperar a fila.</summary>
public sealed class InlineResidentInvitationDispatcher(ISender sender) : IResidentInvitationDispatcher
{
    public async Task EnqueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await sender.Send(new SendResidentInvitationCommand(userId), cancellationToken);
}
