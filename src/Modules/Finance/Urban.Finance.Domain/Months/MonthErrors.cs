using Urban.SharedKernel;

namespace Urban.Finance.Domain.Months;

public static class MonthErrors
{
    public static readonly Error Invalid = new("month.invalid", "Informe um mês válido, no formato AAAA-MM");

    public static readonly Error TooFarAhead = new("month.too_far_ahead", "Só é possível abrir o mês atual, os anteriores e o mês seguinte");
}
