using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Accounts;

/// <summary>
/// Implementação do contrato que entrega o e-mail de uma conta a quem precisa avisá-la. A conta é lida de novo a cada chamada e só
/// responde se ainda pode entrar (ativa) e é de morador ou síndico do condomínio pedido.
/// </summary>
public sealed class AccountEmails(IUserAccounts accounts) : IAccountEmails
{
    public async Task<string?> FindActiveEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(userId, cancellationToken);

        return user is { Status: AccountStatus.Active, Role: UserRole.Resident or UserRole.Manager } && user.CondominiumId == condominiumId
            ? user.Email.Value
            : null;
    }
}
