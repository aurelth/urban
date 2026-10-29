using MediatR;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Registration.RegisterCondominium;

/// <summary>Os dados chegam como o usuário digitou (só texto e uma data); o Domain valida e normaliza cada um.</summary>
public sealed record RegisterCondominiumCommand(
    Guid ManagerUserId,
    string? DisplayName,
    ManagerInput Manager,
    CondominiumInput Condominium) : IRequest<Result<RegisteredCondominium>>;

public sealed record ManagerInput(string? FullName, string? Cpf, DateOnly? BirthDate, string? Phone, AddressInput Address)
{
    /// <summary>O CPF é dado pessoal: um log que imprima o comando não pode vazá-lo.</summary>
    public override string ToString() => $"{nameof(ManagerInput)} {{ FullName = {FullName}, Cpf = *, BirthDate = *, Phone = * }}";
}

public sealed record CondominiumInput(string? Name, string? Cnpj, AddressInput Address);

public sealed record AddressInput(
    string? PostalCode,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State);

/// <summary>O que o cliente recebe: nada de dado pessoal além do CPF mascarado.</summary>
public sealed record RegisteredCondominium(Guid CondominiumId, string Name, string Cnpj, string ManagerCpf);
