using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;

namespace Urban.TestSupport.Condominium;

/// <summary>Monta moradores e vínculos válidos pela API pública do Domain, para os testes mudarem só o campo que querem provar.</summary>
public static class ResidentBuilders
{
    public static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 11, 13);

    public static EmergencyContact SampleEmergencyContact() =>
        EmergencyContact.Create("Carlos Souza", "Irmão", "(47) 3333-4444").Value;

    public static Person Resident(
        Guid? condominiumId = null,
        string name = "Maria Souza",
        Cpf? cpf = null,
        string? email = "maria@exemplo.com") =>
        Person.RegisterResident(
            condominiumId ?? Guid.CreateVersion7(),
            PersonName.Create(name).Value,
            cpf ?? CondominiumBuilders.UniqueCpf(),
            BirthDate.Create(new DateOnly(1990, 5, 20), Today).Value,
            PhoneNumber.Create("(47) 99876-5432").Value,
            email is null ? null : ContactEmail.Create(email).Value,
            Profession.Create("Engenheira").Value,
            SampleEmergencyContact(),
            Now).Value;

    public static Occupancy Occupancy(
        Person person,
        Guid? unitId = null,
        OccupancyKind kind = OccupancyKind.Owner,
        DateOnly? startDate = null) =>
        Urban.Condominium.Domain.Occupancies.Occupancy.Start(
            person.CondominiumId,
            person.Id,
            unitId ?? Guid.CreateVersion7(),
            kind,
            startDate ?? new DateOnly(2024, 3, 1),
            Today,
            Now).Value;
}
