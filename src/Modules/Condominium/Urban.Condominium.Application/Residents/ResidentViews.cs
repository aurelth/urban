using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Monta o <see cref="ResidentView"/>, que leva a situação do acesso. A situação é do Identity (dono das contas), então este é o
/// único lugar que a pergunta, e pergunta em lote: uma consulta por página, só sobre as pessoas que já têm conta, nunca uma por morador.
/// </summary>
public sealed class ResidentViews(IResidentAccess access)
{
    public async Task<ResidentView> ViewAsync(ResidentRecord record, CancellationToken cancellationToken) =>
        (await ViewAllAsync([record], cancellationToken))[0];

    public async Task<IReadOnlyList<ResidentView>> ViewAllAsync(IReadOnlyCollection<ResidentRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        Guid[] userIds = [.. records.Select(r => r.Person.UserId).OfType<Guid>()];
        IReadOnlyDictionary<Guid, ResidentAccessStatus> statuses = userIds.Length == 0
            ? new Dictionary<Guid, ResidentAccessStatus>()
            : await access.GetStatusesAsync(records.First().Person.CondominiumId, userIds, cancellationToken);

        return [.. records.Select(r => ResidentView.From(r, StatusOf(r.Person, statuses)))];
    }

    /// <summary>Quem tem conta que o Identity não conhece (excluída, por exemplo) não entra: nunca se presume ativo.</summary>
    private static ResidentAccessStatus StatusOf(Person person, IReadOnlyDictionary<Guid, ResidentAccessStatus> statuses) =>
        person.UserId is not { } userId
            ? ResidentAccessStatus.None
            : statuses.GetValueOrDefault(userId, ResidentAccessStatus.Inactive);
}
