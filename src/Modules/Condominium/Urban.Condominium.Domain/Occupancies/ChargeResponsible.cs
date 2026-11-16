namespace Urban.Condominium.Domain.Occupancies;

/// <param name="PersonId">A pessoa do vínculo.</param>
/// <param name="Kind">Proprietário, inquilino ou dependente.</param>
/// <param name="Start">Início do vínculo.</param>
/// <param name="End">Último dia do vínculo; nulo enquanto a pessoa mora ou possui a unidade.</param>
public sealed record ResponsibleCandidate(Guid PersonId, OccupancyKind Kind, DateOnly Start, DateOnly? End);

/// <summary>Quem recebe a fatura de uma unidade: o inquilino que mora nela hoje; sem inquilino, o proprietário. Dependente nunca recebe.</summary>
public static class ChargeResponsible
{
    /// <returns>A pessoa escolhida, ou nulo se nenhum vínculo vale hoje. Vale o vínculo que começou até hoje e não terminou antes de hoje. Entre vários do mesmo tipo, o mais recente; em empate, o menor identificador, para a escolha nunca mudar sozinha.</returns>
    public static Guid? Choose(IEnumerable<ResponsibleCandidate> candidates, DateOnly today) =>
        candidates
            .Where(candidate => candidate.Kind != OccupancyKind.Dependent && candidate.Start <= today && (candidate.End is null || candidate.End >= today))
            .OrderBy(candidate => candidate.Kind == OccupancyKind.Tenant ? 0 : 1)
            .ThenByDescending(candidate => candidate.Start)
            .ThenBy(candidate => candidate.PersonId)
            .Select(candidate => (Guid?)candidate.PersonId)
            .FirstOrDefault();
}
