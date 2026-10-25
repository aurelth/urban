using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência do cadastro e da confirmação de e-mail. Cada operação grava por conta própria e de forma atômica.</summary>
public interface IEmailVerifications
{
    /// <summary>Grava a conta pendente e o primeiro link juntos.</summary>
    /// <returns><c>false</c> quando o e-mail já tem conta (outro cadastro ganhou a corrida); nada é gravado.</returns>
    Task<bool> TryRegisterAsync(User user, EmailVerificationToken token, CancellationToken cancellationToken);

    Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken);

    Task<EmailVerificationToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<EmailVerificationToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grava o link usado, a conta ativada e a credencial na mesma transação.</summary>
    /// <returns><c>false</c> quando outra requisição usou o mesmo link antes; nada é gravado.</returns>
    Task<bool> ConfirmAsync(EmailVerificationToken token, User user, UserCredential credential, CancellationToken cancellationToken);
}