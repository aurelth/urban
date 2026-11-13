using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests;

public static class RegistrationRequestErrors
{
    public static readonly Error Forbidden =
        new("identity.registration_request.forbidden", "Só o administrador pode ver e decidir pedidos de cadastro");

    public static readonly Error NotFound =
        new("identity.registration_request.not_found", "Pedido de cadastro não encontrado");
}
