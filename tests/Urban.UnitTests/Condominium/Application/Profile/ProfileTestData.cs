using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Contacts;

namespace Urban.UnitTests.Condominium.Application.Profile;

/// <summary>Entradas de edição que repetem o que já está gravado: cada teste muda só o campo que quer provar.</summary>
internal static class ProfileTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    public const string CurrentDisplayName = "Ana";

    public static AddressInput ToInput(Address address) =>
        new(address.PostalCode.Value, address.Street, address.Number, address.Complement, address.Neighborhood, address.City, address.State);

    public static AddressInput OtherAddress() =>
        new("89036-000", "Rua das Flores", "10", "Casa", "Velha", "Blumenau", "SC");

    public static string Phone(ManagerCondominium registration) => registration.Manager.Phone.Value;
}
