using Urban.SharedKernel;

namespace Urban.Finance.Domain;

public static class FinanceErrors
{
    public static readonly Error CondominiumNotFound = new("finance.condominium_not_found", "Condomínio não encontrado");

    public static readonly Error VersionRequired = new("finance.version.required", "Informe a versão dos dados que você está editando");

    public static readonly Error ConcurrencyConflict = new("concurrency.conflict", "Os dados foram alterados por outra pessoa. Recarregue e tente de novo");
}
