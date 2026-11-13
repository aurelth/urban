using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

public interface IRegistrationRequests
{
    Task<IReadOnlyList<RegistrationRequestSummary>> ListPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Grava só o estado novo da conta (e quem o decidiu), e só se a conta ainda estiver em <paramref name="expectedStatus"/> no banco.
    /// Assim, duas decisões ao mesmo tempo (aprovar e recusar, por exemplo) não se atropelam: uma grava, a outra recebe <c>false</c>.
    /// </summary>
    Task<bool> TryDecideAsync(User user, AccountStatus expectedStatus, CancellationToken cancellationToken);
}
