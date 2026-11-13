namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// O dono do sistema (seção "SuperAdmin"). O mesmo e-mail cria a conta do SuperAdmin na partida da API e recebe o aviso de cada
/// novo pedido de cadastro. Vazio, não há para quem avisar: o pedido continua na lista da tela de administração.
/// </summary>
public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; init; } = string.Empty;
}
