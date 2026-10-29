namespace Urban.SharedKernel.Abstractions;

public enum PostalCodeLookupStatus
{
    Found,
    NotFound,

    /// <summary>Nenhum provedor respondeu com confiança: o CEP pode existir.</summary>
    Unavailable,
}

public sealed record PostalCodeLookupResult
{
    public static readonly PostalCodeLookupResult NotFound = new(PostalCodeLookupStatus.NotFound, null);

    public static readonly PostalCodeLookupResult Unavailable = new(PostalCodeLookupStatus.Unavailable, null);

    private PostalCodeLookupResult(PostalCodeLookupStatus status, PostalCodeAddress? address)
    {
        Status = status;
        Address = address;
    }

    public PostalCodeLookupStatus Status { get; }

    /// <summary>Preenchido só quando <see cref="Status"/> é <see cref="PostalCodeLookupStatus.Found"/>.</summary>
    public PostalCodeAddress? Address { get; }

    public static PostalCodeLookupResult Found(PostalCodeAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new PostalCodeLookupResult(PostalCodeLookupStatus.Found, address);
    }
}
