using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

public static class PersonErrors
{
    public static readonly Error MobileRequired = new("condominium.phone.mobile_required", "Informe um celular com DDD");

    public static readonly Error CondominiumRequired = new("condominium.person.condominium_required", "A pessoa precisa pertencer a um condomínio");

    public static readonly Error UserRequired = new("condominium.person.user_required", "A pessoa precisa estar ligada a um usuário");

    public static readonly Error CpfAlreadyRegistered = new("condominium.cpf.already_registered", "Este CPF já está cadastrado");
}
