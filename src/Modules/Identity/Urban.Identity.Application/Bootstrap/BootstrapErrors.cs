using Urban.SharedKernel;

namespace Urban.Identity.Application.Bootstrap;

public static class BootstrapErrors
{
    /// <summary>O e-mail configurado para o SuperAdmin já é de uma conta com outro papel. Nada é alterado: o bootstrap nunca promove uma conta.</summary>
    public static readonly Error EmailTakenByAnotherRole =
        new("identity.bootstrap.email_taken_by_another_role", "O e-mail do SuperAdmin já pertence a uma conta com outro papel");
}