using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration;

public static class AdministrationErrors
{
    public static readonly Error Forbidden =
        new("identity.administration.forbidden", "Só o administrador pode ver e administrar os síndicos");

    public static readonly Error ManagerNotFound =
        new("identity.manager.not_found", "Síndico não encontrado");

    public static readonly Error PageInvalid =
        new("identity.manager.page_invalid", "A página deve ser 1 ou maior e o tamanho, de 1 a 100");

    public static readonly Error StatusFilterInvalid =
        new("identity.manager.status_filter_invalid", "Filtre por ativo, suspenso, desativado ou aguardando confirmação do e-mail");
}
