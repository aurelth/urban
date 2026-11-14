using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;

namespace Urban.Condominium.Application.Residents;

/// <summary>Um morador como a tela o vê. O CPF só sai mascarado; <c>HasAccount</c> diz se o convite já criou a conta; a versão volta na edição para detectar que outra pessoa mudou o cadastro.</summary>
public sealed record ResidentView(
    Guid Id,
    string FullName,
    string MaskedCpf,
    DateOnly BirthDate,
    string Phone,
    string? Email,
    string? Profession,
    EmergencyContactView? EmergencyContact,
    IReadOnlyList<ResidentOccupancyView> Occupancies,
    bool HasAccount,
    string Version)
{
    internal static ResidentView From(ResidentRecord record)
    {
        Person person = record.Person;

        return new ResidentView(
            person.Id,
            person.FullName.Value,
            person.Cpf.Masked,
            person.BirthDate.Value,
            person.Phone.Value,
            person.Email?.Value,
            person.Profession?.Value,
            person.EmergencyContact is { } contact ? new EmergencyContactView(contact.Name.Value, contact.Relationship, contact.Phone.Value) : null,
            [.. record.Occupancies.Select(ResidentOccupancyView.From)],
            person.UserId is not null,
            record.Version);
    }
}

public sealed record EmergencyContactView(string Name, string Relationship, string Phone);

public sealed record ResidentOccupancyView(
    Guid Id,
    Guid UnitId,
    string? UnitBlock,
    string UnitNumber,
    OccupancyKind Kind,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    internal static ResidentOccupancyView From(ResidentOccupancy item) =>
        new(item.Occupancy.Id, item.Occupancy.UnitId, item.UnitBlock, item.UnitNumber, item.Occupancy.Kind, item.Occupancy.StartDate, item.Occupancy.EndDate);
}
