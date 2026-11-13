using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests.NotifyRegistrationRequest;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o aviso ao dono na hora, dentro da requisição, para os testes de API lerem o e-mail logo após o pedido, sem esperar a fila.</summary>
public sealed class InlineRegistrationRequestDispatcher(ISender sender) : IRegistrationRequestDispatcher
{
    public async Task EnqueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await sender.Send(new NotifyRegistrationRequestCommand(userId), cancellationToken);
}
