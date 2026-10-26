using Urban.SharedKernel;

namespace Urban.Identity.Domain.Consent;

/// <summary>Falhas do aceite dos termos de uso e da política de privacidade. Os códigos são estáveis: o frontend depende deles.</summary>
public static class ConsentErrors
{
    public static readonly Error Required =
        new("identity.consent.required", "Aceite os termos de uso e a política de privacidade para continuar");

    public static readonly Error Outdated =
        new("identity.consent.outdated", "Os termos de uso e a política de privacidade foram atualizados. Recarregue a página e aceite a nova versão");
}