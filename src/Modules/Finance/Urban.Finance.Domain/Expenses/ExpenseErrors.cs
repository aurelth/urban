using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

public static class ExpenseErrors
{
    public static readonly Error DescriptionInvalid = new("expense.description.invalid", "Informe a descrição, com até 120 caracteres");

    public static readonly Error SupplierInvalid = new("expense.supplier.invalid", "O fornecedor pode ter até 120 caracteres");

    public static readonly Error AmountInvalid = new("expense.amount.invalid", "O valor deve ser maior que zero e menor que R$ 10.000.000,00, com até 2 casas decimais");

    public static readonly Error InstallmentCountInvalid = new("expense.installment_count.invalid", "O número de parcelas deve ser de 2 a 120");

    public static readonly Error CurrentInstallmentInvalid = new("expense.current_installment.invalid", "A parcela atual deve estar entre 1 e o número de parcelas");

    public static readonly Error CondominiumRequired = new("expense.condominium_required", "A despesa precisa pertencer a um condomínio");

    public static readonly Error GroupRequired = new("expense.group_required", "Escolha o grupo da despesa");

    public static readonly Error EndBeforeStart = new("expense.end_before_start", "O mês final não pode ser anterior ao mês inicial");

    public static readonly Error ChangeBeforeStart = new("expense.change_before_start", "O novo valor não pode valer a partir de um mês anterior ao início da despesa");

    public static readonly Error NotFixed = new("expense.not_fixed", "Só despesas fixas mensais têm valor com vigência");

    public static readonly Error NotInstallment = new("expense.not_installment", "Só despesas parceladas têm parcelas");

    public static readonly Error InstallmentNotFound = new("expense.installment.not_found", "Parcela não encontrada");

    public static readonly Error EndNotAllowed = new("expense.end_not_allowed", "Só despesas fixas e variáveis podem ser encerradas; as demais terminam sozinhas");

    public static readonly Error NotDueInMonth = new("expense.not_due_in_month", "A despesa não tem lançamento neste mês");

    public static readonly Error KindInvalid = new("expense.kind.invalid", "Escolha se a despesa é única, fixa mensal, variável mensal ou parcelada");

    public static readonly Error NotFound = new("expense.not_found", "Despesa não encontrada");

    public static readonly Error EntryNotFound = new("expense_entry.not_found", "Lançamento não encontrado");
}
