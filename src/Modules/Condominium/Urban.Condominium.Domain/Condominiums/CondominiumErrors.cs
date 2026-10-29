using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Condominiums;

public static class CondominiumErrors
{
    public static readonly Error NameInvalid = new("condominium.name.invalid", "Informe o nome do condomínio, com 2 a 120 caracteres");

    public static readonly Error ManagerRequired = new("condominium.manager_required", "O condomínio precisa de um síndico");

    public static readonly Error CnpjAlreadyRegistered = new("condominium.cnpj.already_registered", "Este CNPJ já está cadastrado. Peça um convite ao síndico atual");

    public static readonly Error ManagerAlreadyRegistered = new("condominium.manager.already_registered", "Você já cadastrou um condomínio");
}
