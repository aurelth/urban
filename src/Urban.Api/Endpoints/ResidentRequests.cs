namespace Urban.Api.Endpoints;

/// <summary>
/// Os dados de um morador, para cadastrar. Datas em AAAA-MM-DD; CPF e telefones só com dígitos (a pontuação é aceita e
/// ignorada). <c>Kind</c> é "owner", "tenant" ou "dependent"; qualquer outro valor responde 400 com o código do campo. O e-mail é opcional:
/// sem ele a pessoa fica cadastrada, mas não pode ser convidada. Quem tem menos de 18 anos só pode ser "dependent" e fica
/// dispensado de <c>Profession</c> e de <c>EmergencyContact</c> (<c>condominium.resident.under_age</c> quando o vínculo é outro).
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

/// <summary>
/// A edição leva todos os dados do cadastro, como a tela os mostra. <c>Version</c> é a que a tela leu. O CPF é opcional: a
/// resposta só mostra o CPF mascarado, então em branco quer dizer "continua o mesmo"; preenchido, troca o CPF.
/// </summary>
public sealed record ResidentUpdateRequest(
    string? Version,
    string? FullName,
    string? Cpf,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Profession,
    EmergencyContactRequest? EmergencyContact);

/// <summary>Um novo vínculo de um morador que já existe. <c>Kind</c> é "owner", "tenant" ou "dependent"; menor de 18 anos só aceita "dependent".</summary>
public sealed record OccupancyRequest(Guid UnitId, string? Kind, DateOnly? StartDate);

/// <summary>A saída do morador de uma unidade: a data (AAAA-MM-DD) vai da entrada até hoje.</summary>
public sealed record OccupancyEndRequest(DateOnly? EndDate);
