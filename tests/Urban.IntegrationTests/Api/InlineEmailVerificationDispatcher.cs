using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Registration.SendEmailVerification;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o envio na hora, dentro da requisição, para os testes de API lerem o e-mail logo após o cadastro, sem esperar a fila.</summary>
public sealed class InlineEmailVerificationDispatcher(ISender sender) : IEmailVerificationDispatcher
{
    public async Task EnqueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await sender.Send(new SendEmailVerificationCommand(userId), cancellationToken);
}