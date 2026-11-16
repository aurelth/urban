using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

/// <summary>As contas de uma cobrança: quanto já foi pago, quanto falta e em que situação ela está. Só números, sem estado.</summary>
public static class ChargeAccount
{
    /// <summary>A soma dos pagamentos que não foram estornados.</summary>
    public static decimal Paid(IEnumerable<Payment> payments) => payments.Where(payment => !payment.IsReversed).Sum(payment => payment.Amount);

    public static decimal Balance(decimal chargeAmount, decimal paid) => chargeAmount - paid;

    /// <summary>
    /// Pago quando o total foi alcançado; senão atrasada depois do vencimento (o dia do vencimento ainda vale); senão parcial
    /// se algo foi pago; senão em aberto.
    /// </summary>
    public static ChargeStatus StatusOf(decimal chargeAmount, decimal paid, DateOnly dueDate, DateOnly today)
    {
        if (paid >= chargeAmount)
        {
            return ChargeStatus.Paid;
        }

        if (today > dueDate)
        {
            return ChargeStatus.Overdue;
        }

        return paid > 0 ? ChargeStatus.Partial : ChargeStatus.Open;
    }

    /// <summary>Um pagamento pode chegar ao saldo, nunca passar dele: não existe crédito.</summary>
    public static Result EnsureFits(decimal chargeAmount, decimal paid, decimal amount)
    {
        decimal balance = Balance(chargeAmount, paid);

        return amount <= balance ? Result.Success() : Result.Failure(PaymentErrors.ExceedsBalance(balance));
    }
}
