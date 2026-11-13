namespace Urban.Api.Http;

public static class RateLimitPolicies
{
    public const string Register = "register";

    public const string ConfirmEmail = "confirm-email";

    public const string Login = "login";

    public const string ForgotPassword = "forgot-password";

    public const string ResetPassword = "reset-password";

    public const string Refresh = "refresh";

    public const string Logout = "logout";

    public const string Me = "me";

    public const string Terms = "terms";

    public const string CondominiumRegistration = "condominium-registration";

    public const string PostalCodeLookup = "postal-code-lookup";

    public const string CondominiumProfile = "condominium-profile";

    /// <summary>Todas as rotas de unidades, exceto o cadastro em lote.</summary>
    public const string Units = "units";

    /// <summary>Cadastro de até 500 unidades de uma vez: grava muito, então o limite é bem menor.</summary>
    public const string UnitBatch = "unit-batch";

    /// <summary>Todas as rotas de moradores e vínculos com as unidades.</summary>
    public const string Residents = "residents";

    /// <summary>Todas as rotas do financeiro: despesas, grupos e lançamentos do mês.</summary>
    public const string Finance = "finance";

    /// <summary>Rotas de administração do SuperAdmin: pedidos de cadastro.</summary>
    public const string Admin = "admin";

    /// <summary>Envio de mensagem do síndico ao dono: grava e dispara e-mail, então o limite é menor para não virar canal de spam.</summary>
    public const string Contact = "contact";

    /// <summary>Resposta do dono a uma mensagem: envia e-mail com anexos de até 10 MB, então o limite é menor.</summary>
    public const string ContactReply = "contact-reply";

    /// <summary>Troca de senha, de e-mail e edição dos dados sensíveis: ações que exigem a senha, com limite menor.</summary>
    public const string SensitiveChange = "sensitive-change";

    /// <summary>Código do problema devolvido com o 429, no mesmo campo "code" dos erros de negócio.</summary>
    public const string ExceededCode = "rate_limit.exceeded";
}