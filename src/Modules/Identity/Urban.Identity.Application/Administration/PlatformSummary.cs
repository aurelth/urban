namespace Urban.Identity.Application.Administration;

/// <param name="AwaitingConfirmationManagers">Pedidos aprovados cujo dono ainda não confirmou o e-mail.</param>
/// <param name="PendingRequests">Pedidos de cadastro esperando a decisão do administrador.</param>
/// <param name="Condominiums">Condomínios que já têm um síndico (um condomínio conta uma vez).</param>
public sealed record PlatformSummary(
    int ActiveManagers,
    int SuspendedManagers,
    int AwaitingConfirmationManagers,
    int PendingRequests,
    int Condominiums);
