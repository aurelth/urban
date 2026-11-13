using Urban.Identity.Application.Administration;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência das contas de síndico para o painel do administrador.</summary>
public interface IManagerAccounts
{
    /// <summary>Contas de síndico nos estados de <see cref="ManagerStatuses.Listed"/>, as mais novas primeiro, já recortadas pela página (a ordem e o recorte são do banco).</summary>
    Task<ManagerPage> ListAsync(ManagerFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    Task<PlatformSummary> SummarizeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Grava só o estado novo da conta (e quem o mudou e por quê), e só se a conta ainda estiver em <paramref name="expectedStatus"/> no banco.
    /// Assim, duas decisões ao mesmo tempo (suspender e desativar, por exemplo) não se atropelam: uma grava, a outra recebe <c>false</c>.
    /// </summary>
    Task<bool> TryChangeStatusAsync(User user, AccountStatus expectedStatus, CancellationToken cancellationToken);
}
