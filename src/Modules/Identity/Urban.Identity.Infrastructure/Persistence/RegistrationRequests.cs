using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>Leitura e decisão dos pedidos de cadastro. A decisão é um único UPDATE condicionado ao estado esperado, que o banco aplica de forma atômica.</summary>
public sealed class RegistrationRequests(IdentityDbContext db) : IRegistrationRequests
{
    public async Task<IReadOnlyList<RegistrationRequestSummary>> ListPendingAsync(CancellationToken cancellationToken)
    {
        // O e-mail e os nomes têm conversores de valor: a projeção para o resumo acontece em memória, depois da consulta.
        List<User> pending = await db.Users
            .AsNoTracking()
            .Where(u => u.Status == AccountStatus.PendingApproval)
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

        return pending.Select(RegistrationRequestSummary.From).ToList();
    }

    public async Task<bool> TryDecideAsync(User user, AccountStatus expectedStatus, CancellationToken cancellationToken)
    {
        int changed = await db.Users
            .Where(u => u.Id == user.Id && u.Status == expectedStatus)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(u => u.Status, user.Status)
                    .SetProperty(u => u.StatusChangedAt, user.StatusChangedAt)
                    .SetProperty(u => u.StatusChangedBy, user.StatusChangedBy)
                    .SetProperty(u => u.StatusReason, user.StatusReason),
                cancellationToken);

        return changed == 1;
    }
}
