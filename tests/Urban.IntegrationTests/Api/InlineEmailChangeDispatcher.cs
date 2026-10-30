using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.EmailChange.SendEmailChange;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o envio do link na hora, dentro da requisição, para os testes lerem o e-mail logo após o pedido, sem esperar a fila.</summary>
public sealed class InlineEmailChangeDispatcher(ISender sender) : IEmailChangeDispatcher
{
    public async Task EnqueueAsync(Guid userId, Email newEmail, CancellationToken cancellationToken) =>
        await sender.Send(new SendEmailChangeCommand(userId, newEmail.Value), cancellationToken);
}
