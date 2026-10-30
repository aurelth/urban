using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;

namespace Urban.Condominium.Application.Profile;

/// <summary>O que o síndico vê do próprio cadastro. O CPF só aparece mascarado; o número inteiro nunca sai do módulo.</summary>
/// <param name="Version">Devolvida na edição para detectar que outra pessoa mudou os dados no meio do caminho.</param>
/// <param name="DisplayName">Como o síndico prefere ser chamado (vem da conta); nulo quando o resultado de uma edição de identidade não a relê.</param>
public sealed record MyCondominium(Guid CondominiumId, string Version, string? DisplayName, ManagerView Manager, CondominiumView Condominium)
{
    internal static MyCondominium From(ManagerCondominium registration, string? displayName, string? version = null) =>
        new(
            registration.Condominium.Id,
            version ?? registration.Version,
            displayName,
            new ManagerView(
                registration.Manager.FullName.Value,
                registration.Manager.Cpf.Masked,
                registration.Manager.BirthDate.Value,
                registration.Manager.Phone.Value,
                AddressView.From(registration.Manager.Address)),
            new CondominiumView(
                registration.Condominium.Name,
                registration.Condominium.Cnpj.Formatted,
                AddressView.From(registration.Condominium.Address)));
}

public sealed record ManagerView(string FullName, string MaskedCpf, DateOnly BirthDate, string Phone, AddressView Address);

public sealed record CondominiumView(string Name, string Cnpj, AddressView Address);

public sealed record AddressView(string PostalCode, string Street, string Number, string? Complement, string Neighborhood, string City, string State)
{
    internal static AddressView From(Address address) =>
        new(address.PostalCode.Value, address.Street, address.Number, address.Complement, address.Neighborhood, address.City, address.State);
}
