using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

public static class ResidentErrors
{
    public static readonly Error NotFound = new("condominium.resident.not_found", "Morador não encontrado");

    public static readonly Error UnderAge = new("condominium.resident.under_age", "O morador precisa ter 18 anos ou mais");

    public static readonly Error EmailRequiredForInvitation =
        new("condominium.resident.email_required", "Cadastre o e-mail do morador para poder convidá-lo");

    public static readonly Error EmailLocked =
        new("condominium.resident.email_locked", "O e-mail não pode ser alterado depois que o morador recebe o convite");

    public static readonly Error PageInvalid = new("condominium.resident.page_invalid", "A página deve ser 1 ou maior e o tamanho, de 1 a 200");
}
