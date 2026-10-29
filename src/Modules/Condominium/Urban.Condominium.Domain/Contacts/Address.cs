using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Contacts;

public sealed record Address
{
    private const int StreetMaxLength = 120;
    private const int NumberMaxLength = 10;
    private const int ComplementMaxLength = 60;
    private const int NeighborhoodMaxLength = 80;
    private const int CityMaxLength = 80;

    public static readonly IReadOnlyCollection<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    public static readonly Error StreetInvalid = new("condominium.address.street_invalid", "Informe a rua");
    public static readonly Error NumberInvalid = new("condominium.address.number_invalid", "Informe o número (use S/N quando não houver)");
    public static readonly Error ComplementInvalid = new("condominium.address.complement_invalid", "O complemento deve ter até 60 caracteres");
    public static readonly Error NeighborhoodInvalid = new("condominium.address.neighborhood_invalid", "Informe o bairro");
    public static readonly Error CityInvalid = new("condominium.address.city_invalid", "Informe a cidade");
    public static readonly Error StateInvalid = new("condominium.address.state_invalid", "Informe a UF");

    private Address(
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        PostalCode postalCode)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        PostalCode = postalCode;
    }

    public string Street { get; }

    public string Number { get; }

    public string? Complement { get; }

    public string Neighborhood { get; }

    public string City { get; }

    public string State { get; }

    public PostalCode PostalCode { get; }

    public static Result<Address> Create(
        string? street,
        string? number,
        string? complement,
        string? neighborhood,
        string? city,
        string? state,
        string? postalCode)
    {
        string normalizedStreet = TextRules.Normalize(street);
        string normalizedNumber = TextRules.Normalize(number);
        string normalizedComplement = TextRules.Normalize(complement);
        string normalizedNeighborhood = TextRules.Normalize(neighborhood);
        string normalizedCity = TextRules.Normalize(city);
        string normalizedState = TextRules.Normalize(state).ToUpperInvariant();

        if (!IsText(normalizedStreet, StreetMaxLength))
        {
            return Result.Failure<Address>(StreetInvalid);
        }

        if (!IsText(normalizedNumber, NumberMaxLength))
        {
            return Result.Failure<Address>(NumberInvalid);
        }

        if (normalizedComplement.Length > 0 && !IsText(normalizedComplement, ComplementMaxLength))
        {
            return Result.Failure<Address>(ComplementInvalid);
        }

        if (!IsText(normalizedNeighborhood, NeighborhoodMaxLength))
        {
            return Result.Failure<Address>(NeighborhoodInvalid);
        }

        if (!IsText(normalizedCity, CityMaxLength))
        {
            return Result.Failure<Address>(CityInvalid);
        }

        if (!States.Contains(normalizedState))
        {
            return Result.Failure<Address>(StateInvalid);
        }

        Result<PostalCode> code = PostalCode.Create(postalCode);

        if (code.IsFailure)
        {
            return Result.Failure<Address>(code.Error);
        }

        return Result.Success(new Address(
            normalizedStreet,
            normalizedNumber,
            normalizedComplement.Length == 0 ? null : normalizedComplement,
            normalizedNeighborhood,
            normalizedCity,
            normalizedState,
            code.Value));
    }

    private static bool IsText(string value, int maxLength) =>
        value.Length > 0 && value.Length <= maxLength && !TextRules.HasControlCharacters(value);
}
