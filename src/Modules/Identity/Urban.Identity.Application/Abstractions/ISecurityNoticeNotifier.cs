using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que entrega o aviso de segurança. Texto, endereço do site e canal de envio são detalhes da Infrastructure.</summary>
public interface ISecurityNoticeNotifier
{
    Task SendAsync(SecurityNotice notice, Email recipient, Email? other, CancellationToken cancellationToken);
}
