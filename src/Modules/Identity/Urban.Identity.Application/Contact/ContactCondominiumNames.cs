using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Contact;

internal static class ContactCondominiumNames
{
    /// <summary>Uma única consulta ao módulo Condomínio para todos os identificadores, sem repetição; sem identificadores, nem consulta.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        ICondominiumDirectory condominiums,
        IEnumerable<Guid?> condominiumIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. condominiumIds.OfType<Guid>().Distinct()];

        return ids.Length == 0 ? new Dictionary<Guid, string>() : await condominiums.GetNamesAsync(ids, cancellationToken);
    }

    public static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? condominiumId) =>
        condominiumId is { } id && names.TryGetValue(id, out string? name) ? name : null;
}
