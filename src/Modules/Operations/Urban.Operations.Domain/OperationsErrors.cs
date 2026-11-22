using Urban.SharedKernel;

namespace Urban.Operations.Domain;

public static class OperationsErrors
{
    public static readonly Error CondominiumNotFound = new("operations.condominium_not_found", "Condomínio não encontrado");

    public static readonly Error VersionRequired = new("operations.version.required", "Informe a versão dos dados que você está editando");

    public static readonly Error ConcurrencyConflict = new("concurrency.conflict", "Os dados foram alterados por outra pessoa. Recarregue e tente de novo");
}
