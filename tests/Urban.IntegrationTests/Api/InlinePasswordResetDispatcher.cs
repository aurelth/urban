using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.PasswordReset.SendPasswordReset;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o envio na hora, dentro da requisição, para os testes de API lerem o e-mail logo após o pedido, sem esperar a fila.</summary>
public sealed class InlinePasswordResetDispatcher(ISender sender) : IPasswordResetDispatcher
{
    public async Task EnqueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await sender.Send(new SendPasswordResetCommand(userId), cancellationToken);
}