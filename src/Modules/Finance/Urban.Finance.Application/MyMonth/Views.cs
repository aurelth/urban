using Urban.Finance.Domain.Payments;

namespace Urban.Finance.Application.MyMonth;

/// <summary>Como a conta mora no condomínio hoje: com unidade para cobrar, só como dependente, ou sem vínculo nenhum.</summary>
public enum MyMonthAccess
{
    Unit,
    Dependent,
    None,
}

/// <summary>Um pagamento da cobrança como o morador o vê: o valor, o dia, a forma e o recibo. Sem observação nem motivo do estorno, que ficam com o síndico.</summary>
/// <param name="Reversed">Se o pagamento foi estornado; o recibo dele continua existindo, marcado como cancelado.</param>
public sealed record MyPaymentView(Guid PaymentId, decimal Amount, DateOnly ReceivedOn, PaymentMethod Method, int ReceiptNumber, bool Reversed);

/// <summary>A cobrança da unidade no mês, com a situação calculada hoje e o que já foi pago (sem estornos).</summary>
/// <param name="Payments">Todos os pagamentos da cobrança, estornados inclusive, do mais antigo para o mais novo.</param>
public sealed record MyChargeView(
    Guid ChargeId, DateOnly DueDate, decimal Amount, decimal Paid, decimal Balance, ChargeStatus Status, DateOnly? LastReceivedOn, IReadOnlyList<MyPaymentView> Payments);

/// <param name="Charge">Nulo enquanto o mês não fechou, ou se a unidade não recebeu cobrança nele.</param>
public sealed record MyUnitView(Guid UnitId, string? Block, string Number, MyChargeView? Charge);

/// <summary>Um grupo de despesa do mês, só com o total: fornecedor, observação e situação de cada lançamento ficam com o síndico.</summary>
public sealed record MyExpenseGroupView(Guid GroupId, string GroupName, decimal Total);

public sealed record MyExpensesView(decimal Total, IReadOnlyList<MyExpenseGroupView> Groups);

/// <param name="Closed">Se o mês já foi fechado.</param>
/// <param name="Units">As unidades da conta; vazio para dependente e para quem não tem vínculo.</param>
/// <param name="Expenses">Só de mês fechado e só para quem tem unidade: com o mês aberto os valores ainda mudam.</param>
public sealed record MyMonthView(string Month, DateOnly AsOf, MyMonthAccess Access, bool Closed, IReadOnlyList<MyUnitView> Units, MyExpensesView? Expenses);
