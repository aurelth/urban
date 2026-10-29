namespace Urban.SharedKernel.Abstractions;

/// <summary>
/// O condomínio (tenant) da requisição em andamento. A API o lê do token de acesso; os módulos o usam para filtrar
/// os dados. Nulo quando a requisição não tem condomínio: SuperAdmin, síndico que ainda não cadastrou o dele ou quem
/// não está autenticado. Nesse caso os filtros por condomínio não devolvem nada.
/// </summary>
public interface ITenantContext
{
    Guid? CondominiumId { get; }
}
