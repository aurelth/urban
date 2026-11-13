using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Administration;

/// <param name="Status">Só este estado; nulo mostra todos os que a lista cobre.</param>
/// <param name="Search">Trecho do e-mail, do nome ou do nome do condomínio informado no pedido; sem diferenciar maiúsculas.</param>
public sealed record ManagerFilter(AccountStatus? Status, string? Search);
