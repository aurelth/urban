using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta que pede o envio de um aviso de segurança em segundo plano. O aviso nunca leva senha nem link.</summary>
public interface ISecurityNoticeDispatcher
{
    /// <param name="notice">O que aconteceu.</param>
    /// <param name="recipient">Para quem o aviso vai.</param>
    /// <param name="other">O outro endereço citado no texto, quando o aviso fala de uma troca de e-mail.</param>
    Task EnqueueAsync(SecurityNotice notice, Email recipient, Email? other, CancellationToken cancellationToken);
}
