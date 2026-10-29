using Urban.SharedKernel;

namespace Urban.Condominium.Application.PostalCodes;

public static class PostalCodeErrors
{
    public static readonly Error NotFound = new(
        "condominium.postal_code.not_found",
        "Não encontramos esse CEP. Preencha o endereço manualmente");

    public static readonly Error LookupUnavailable = new(
        "condominium.postal_code.lookup_unavailable",
        "Não foi possível consultar o CEP agora. Preencha o endereço manualmente");
}
