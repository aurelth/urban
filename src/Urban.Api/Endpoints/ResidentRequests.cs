namespace Urban.Api.Endpoints;

/// <summary>
/// Os dados de um morador adulto, para cadastrar. Datas em AAAA-MM-DD; CPF e telefones só com dígitos (a pontuação é aceita e
/// ignorada). <c>Kind</c> é "owner" ou "tenant"; qualquer outro valor responde 400 com o código do campo. O e-mail é opcional:
/// sem ele a pessoa fica cadastrada, mas não pode ser convidada.
/// </summary>
public sealed record ResidentRequest(
    string? FullName,
    string? Cpf,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Profession,
    EmergencyContactRequest? EmergencyContact,
    Guid UnitId,
    string? Kind,
    DateOnly? StartDate);

public sealed record EmergencyContactRequest(string? Name, string? Relationship, string? Phone);
