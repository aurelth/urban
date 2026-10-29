using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Condominiums;

public static class CondominiumErrors
{
    public static readonly Error NameInvalid = new("condominium.name.invalid", "Informe o nome do condomínio, com 2 a 120 caracteres");

    public static readonly Error ManagerRequired = new("condominium.manager_required", "O condomínio precisa de um síndico");
}
