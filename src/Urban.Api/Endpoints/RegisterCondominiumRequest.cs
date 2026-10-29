namespace Urban.Api.Endpoints;

/// <summary>Só dígitos nos documentos, no CEP e no telefone (as máscaras são da tela); a data de nascimento vai como AAAA-MM-DD.</summary>
public sealed record RegisterCondominiumRequest(string? DisplayName, ManagerRequest? Manager, CondominiumRequest? Condominium);

public sealed record ManagerRequest(string? FullName, string? Cpf, DateOnly? BirthDate, string? Phone, AddressRequest? Address)
{
    /// <summary>O CPF é dado pessoal: um log que imprima o pedido não pode vazá-lo.</summary>
    public override string ToString() => $"{nameof(ManagerRequest)} {{ FullName = {FullName}, Cpf = *, BirthDate = *, Phone = * }}";
}

public sealed record CondominiumRequest(string? Name, string? Cnpj, AddressRequest? Address);

public sealed record AddressRequest(
    string? PostalCode,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State);
