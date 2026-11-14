namespace Urban.Condominium.Domain.Occupancies;

/// <summary>
/// Qual é o vínculo da pessoa com a unidade. Gravado como texto (o nome do valor); o 0 nunca vale. Quem tem menos de 18 anos
/// só pode ser <see cref="Dependent"/>; um adulto também pode (um parente que mora junto, por exemplo).
/// </summary>
public enum OccupancyKind
{
    Owner = 1,
    Tenant = 2,
    Dependent = 3,
}
