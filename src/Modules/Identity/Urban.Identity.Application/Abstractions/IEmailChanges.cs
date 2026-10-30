using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência da troca de e-mail. Cada operação grava por conta própria e de forma atômica.</summary>
public interface IEmailChanges
{
    /// <summary>Grava o novo link e dá por usados os outros links ainda abertos da conta: vale sempre o último pedido.</summary>
    Task AddAsync(EmailChangeToken token, CancellationToken cancellationToken);

    Task<EmailChangeToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<EmailChangeToken?> FindLatestOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Grava, na mesma transação, o link já usado, o novo e-mail da conta e o uso dos outros links abertos dela.
    /// O link precisa ter sido consumido antes e o usuário precisa ter o e-mail já trocado em memória.
    /// </summary>
    /// <returns><c>false</c> quando outra requisição usou o link ou alterou a conta antes, ou quando o endereço já virou de outra conta; nada é gravado.</returns>
    Task<bool> ConfirmAsync(EmailChangeToken token, User user, CancellationToken cancellationToken);
}
