using Urban.Identity.Application.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Accounts;

/// <summary>Implementação do contrato que entrega o e-mail de uma conta ativa a quem precisa avisá-la. A conta é lida de novo a cada chamada.</summary>
public sealed class AccountEmails(IUserAccounts accounts) : IAccountEmails
{
    public Task<string?> FindActiveEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {accounts.GetType().Name} {userId} {condominiumId}");
}
