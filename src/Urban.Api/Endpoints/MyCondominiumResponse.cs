namespace Urban.Api.Endpoints;

/// <summary>O cadastro do síndico como a tela edita. O CPF só volta mascarado; o número inteiro nunca sai da API.</summary>
public sealed record MyCondominiumResponse(
    Guid CondominiumId,
    string Version,
    string? DisplayName,
    ManagerDataResponse Manager,
    CondominiumDataResponse Condominium);

public sealed record ManagerDataResponse(string FullName, string MaskedCpf, DateOnly BirthDate, string Phone, AddressResponse Address);

public sealed record CondominiumDataResponse(string Name, string Cnpj, AddressResponse Address);

public sealed record AddressResponse(string PostalCode, string Street, string Number, string? Complement, string Neighborhood, string City, string State);
