using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

public static class ExpenseGroupErrors
{
    public static readonly Error NameInvalid = new("expense_group.name.invalid", "Informe o nome do grupo, com até 60 caracteres");

    public static readonly Error CondominiumRequired = new("expense_group.condominium_required", "O grupo precisa pertencer a um condomínio");

    public static readonly Error NotFound = new("expense_group.not_found", "Grupo de despesas não encontrado");

    public static readonly Error NameAlreadyExists = new("expense_group.name.already_exists", "Já existe um grupo com este nome");
}
