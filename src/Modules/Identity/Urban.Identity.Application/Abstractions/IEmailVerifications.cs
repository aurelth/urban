using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência do cadastro e da confirmação de e-mail. Cada operação grava por conta própria e de forma atômica.</summary>
public interface IEmailVerifications
{
    /// <summary>Grava a conta pendente. O link vem depois, emitido pelo envio em segundo plano.</summary>
    /// <returns><c>false</c> quando o e-mail já tem conta (outro cadastro ganhou a corrida); nada é gravado.</returns>
    Task<bool> TryRegisterAsync(User user, CancellationToken cancellationToken);

    Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken);

    Task<EmailVerificationToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<EmailVerificationToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grava o link usado, a conta ativada, a credencial e o aceite dos termos na mesma transação.</summary>
    /// <returns><c>false</c> quando outra requisição usou o mesmo link antes; nada é gravado.</returns>
    Task<bool> ConfirmAsync(
        EmailVerificationToken token,
        User user,
        UserCredential credential,
        UserConsent consent,
        CancellationToken cancellationToken);
}