namespace Urban.Condominium.Domain.Occupancies;

/// <summary>
/// Qual é o vínculo da pessoa com a unidade. Gravado como texto (o nome do valor); o 0 nunca vale. Dependente entra junto
/// com o módulo de dependentes.
/// </summary>
public enum OccupancyKind
{
    Owner = 1,
    Tenant = 2,
}
