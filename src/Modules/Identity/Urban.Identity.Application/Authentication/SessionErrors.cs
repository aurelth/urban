using Urban.SharedKernel;

namespace Urban.Identity.Application.Authentication;

public static class SessionErrors
{
    public static readonly Error Unauthenticated =
        new("identity.session.unauthenticated", "Sessão inválida ou expirada. Entre novamente");

    public static readonly Error StoreUnavailable =
        new("identity.session.store_unavailable", "Serviço temporariamente indisponível. Tente novamente em instantes");
}