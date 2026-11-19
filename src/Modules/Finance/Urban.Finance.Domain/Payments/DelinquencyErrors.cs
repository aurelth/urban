using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

public static class DelinquencyErrors
{
    public static readonly Error PageInvalid = new("finance.delinquency.page_invalid", "A página deve ser 1 ou maior e o tamanho da página deve ficar entre 1 e 200");
}
