using Urban.SharedKernel;

namespace Urban.Finance.Domain.Closing;

public static class ClosingErrors
{
    public static readonly Error TotalInvalid = new("closing.total_invalid", "O total do mês deve ter no máximo 2 casas decimais e não passar de R$ 9.999.999,99");

    public static readonly Error NoWeights = new("closing.no_weights", "O rateio precisa de ao menos uma unidade com peso maior que zero, e nenhum peso pode ser negativo");

    public static readonly Error MonthNotOpen = new("closing.month_not_open", "Abra o mês antes de fechar: ele ainda não tem lançamentos");

    public static readonly Error AmountsPending = new("closing.amounts_pending", "Informe o valor de todos os lançamentos variáveis do mês antes de fechar");

    public static readonly Error NothingToCharge = new("closing.nothing_to_charge", "O mês não tem nada a cobrar: todos os lançamentos estão cancelados");

    public static readonly Error DueDateInvalid = new("closing.due_date_invalid", "O vencimento das cobranças não pode ser antes do primeiro dia do mês");

    public static readonly Error AlreadyClosed = new("closing.already_closed", "Este mês já está fechado");

    public static readonly Error NotClosed = new("closing.not_closed", "Este mês não está fechado");

    public static readonly Error MonthIsClosed = new("closing.month_is_closed", "O mês está fechado. Reabra o mês para mudar os lançamentos");
}
