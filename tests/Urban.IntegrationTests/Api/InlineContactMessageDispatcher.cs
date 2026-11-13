using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact.NotifyContactMessage;

namespace Urban.IntegrationTests.Api;

/// <summary>Executa o aviso ao dono na hora, dentro da requisição, para os testes de API lerem o e-mail logo após o envio, sem esperar a fila.</summary>
public sealed class InlineContactMessageDispatcher(ISender sender) : IContactMessageDispatcher
{
    public async Task EnqueueAsync(Guid messageId, CancellationToken cancellationToken) =>
        await sender.Send(new NotifyContactMessageCommand(messageId), cancellationToken);
}
