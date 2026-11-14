using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Occupancies;

namespace Urban.TestSupport.Condominium;

/// <summary>Comandos de cadastro de morador prontos para os testes: tudo válido, com CPF único a cada chamada.</summary>
public static class ResidentCommands
{
    public static RegisterResidentCommand Valid(
        Guid unitId,
        Guid? actorUserId = null,
        string? cpf = null,
        string? email = "maria@exemplo.com",
        OccupancyKind kind = OccupancyKind.Owner) =>
        new(
            actorUserId ?? Guid.CreateVersion7(),
            "Maria Souza",
            cpf ?? CondominiumBuilders.UniqueCpf().Value,
            new DateOnly(1990, 5, 20),
            "(47) 99876-5432",
            email,
            "Engenheira",
            new EmergencyContactInput("Carlos Souza", "Irmão", "(47) 3333-4444"),
            unitId,
            kind,
            new DateOnly(2024, 3, 1));

    /// <summary>A edição de um morador criado por <see cref="ResidentBuilders.Resident"/>: repete os mesmos dados, para cada teste mudar só um campo.</summary>
    public static UpdateResidentCommand Update(Guid residentId, string? version = "4", Guid? actorUserId = null, string? cpf = null, string? email = "maria@exemplo.com") =>
        new(
            actorUserId ?? Guid.CreateVersion7(),
            residentId,
            version,
            "Maria Souza",
            cpf,
            new DateOnly(1990, 5, 20),
            "(47) 99876-5432",
            email,
            "Engenheira",
            new EmergencyContactInput("Carlos Souza", "Irmão", "(47) 3333-4444"));

    /// <summary>O cadastro de um dependente menor de idade (11 anos em 13/11/2026): sem profissão, sem contato de emergência e sem e-mail.</summary>
    public static RegisterResidentCommand Minor(Guid unitId, Guid? actorUserId = null, OccupancyKind kind = OccupancyKind.Dependent) =>
        Valid(unitId, actorUserId, email: null, kind: kind) with
        {
            FullName = "Pedro Souza",
            BirthDate = new DateOnly(2015, 4, 10),
            Profession = null,
            EmergencyContact = new EmergencyContactInput(null, null, null),
        };

    /// <summary>A edição de um dependente criado por <see cref="ResidentBuilders.Minor"/>: repete os dados dele, sem profissão nem contato.</summary>
    public static UpdateResidentCommand UpdateMinor(Guid residentId, string? version = "4", string? cpf = null) =>
        Update(residentId, version, cpf: cpf, email: null) with
        {
            FullName = "Pedro Souza",
            BirthDate = new DateOnly(2015, 4, 10),
            Profession = null,
            EmergencyContact = new EmergencyContactInput(null, null, null),
        };
}
