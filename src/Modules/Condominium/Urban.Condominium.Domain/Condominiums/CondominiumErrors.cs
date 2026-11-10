using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Condominiums;

public static class CondominiumErrors
{
    public static readonly Error NameInvalid = new("condominium.name.invalid", "Informe o nome do condomínio, com 2 a 120 caracteres");

    public static readonly Error ManagerRequired = new("condominium.manager_required", "O condomínio precisa de um síndico");

    public static readonly Error CnpjAlreadyRegistered = new("condominium.cnpj.already_registered", "Este CNPJ já está cadastrado. Peça um convite ao síndico atual");

    public static readonly Error ManagerAlreadyRegistered = new("condominium.manager.already_registered", "Você já cadastrou um condomínio");

    public static readonly Error NotFound = new("condominium.not_found", "Condomínio não encontrado");

    public static readonly Error ConcurrencyConflict = new("concurrency.conflict", "Os dados foram alterados por outra pessoa. Recarregue e tente de novo");

    public static readonly Error VersionRequired = new("condominium.version.required", "Informe a versão dos dados que você está editando");

    public static readonly Error ApportionmentMethodInvalid = new("condominium.apportionment_method.invalid", "Escolha fração ideal, valor igual ou área");

    public static readonly Error ApportionmentNotReady = new("condominium.apportionment_not_ready", "Os dados das unidades ainda não bastam para o método de rateio escolhido. Complete-os na tela de unidades");

    /// <summary>Quantas unidades o erro nomeia: o resto vai só na contagem, para a resposta não crescer com o prédio.</summary>
    private const int MaxNamedUnits = 5;

    /// <summary>
    /// O mesmo erro, com o que falta: <c>reason</c> (<c>no_active_units</c>, <c>fractions_missing</c>, <c>fractions_total</c> ou
    /// <c>areas_missing</c>), a soma atual em <c>fractionTotal</c> e as primeiras unidades sem o valor em <c>units</c>, com o total em <c>unitCount</c>.
    /// </summary>
    public static Error ApportionmentNotReadyFor(ApportionmentShortfall shortfall)
    {
        Dictionary<string, object?> data = new() { ["reason"] = ReasonCode(shortfall.Reason) };
        if (shortfall.FractionTotal is { } total)
        {
            data["fractionTotal"] = total;
        }

        if (shortfall.Units.Count > 0)
        {
            data["unitCount"] = shortfall.Units.Count;
            data["units"] = shortfall.Units.Take(MaxNamedUnits).Select(unit => new { block = unit.Block, number = unit.Number }).ToList();
        }

        return ApportionmentNotReady with { Data = data };
    }

    private static string ReasonCode(ApportionmentShortfallReason reason) => reason switch
    {
        ApportionmentShortfallReason.NoActiveUnits => "no_active_units",
        ApportionmentShortfallReason.FractionsMissing => "fractions_missing",
        ApportionmentShortfallReason.FractionsTotal => "fractions_total",
        ApportionmentShortfallReason.AreasMissing => "areas_missing",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Motivo desconhecido"),
    };
}
