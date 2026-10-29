using Urban.SharedKernel;

namespace Urban.Api.Http;

public static class RequestErrors
{
    public static readonly Error Invalid = new("request.invalid", "Não foi possível ler os dados enviados. Confira o formato e tente de novo");
}
