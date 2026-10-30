using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>Entrega o aviso de segurança na hora, dentro da requisição, para os testes lerem o e-mail sem esperar a fila.</summary>
public sealed class InlineSecurityNoticeDispatcher(ISecurityNoticeNotifier notifier) : ISecurityNoticeDispatcher
{
    public Task EnqueueAsync(SecurityNotice notice, Email to, Email? other, CancellationToken cancellationToken) =>
        notifier.SendAsync(notice, to, other, cancellationToken);
}
