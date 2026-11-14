using MediatR;
using Urban.Condominium.Domain.Occupancies;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Os dados chegam como a pessoa digitou (só texto e datas); o Domain valida e normaliza cada um. O condomínio não vem no
/// comando: é o do token, e o síndico só cadastra no dele.
/// </summary>
public sealed record RegisterResidentCommand(
    Guid ActorUserId,
    string? FullName,
    string? Cpf,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Profession,
    EmergencyContactInput EmergencyContact,
    Guid UnitId,
    OccupancyKind Kind,
    DateOnly? StartDate) : IRequest<Result<ResidentView>>
{
    /// <summary>O CPF e os demais dados pessoais não podem aparecer num log que imprima o comando.</summary>
    public override string ToString() => $"{nameof(RegisterResidentCommand)} {{ UnitId = {UnitId}, Kind = {Kind} }}";
}

public sealed record EmergencyContactInput(string? Name, string? Relationship, string? Phone)
{
    /// <summary>Nenhum dos três campos foi preenchido: é o que o dependente menor de idade manda.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Relationship) && string.IsNullOrWhiteSpace(Phone);

    public override string ToString() => nameof(EmergencyContactInput);
}
