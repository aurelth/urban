namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Em que pé está o acesso de um morador ao sistema, do ponto de vista do síndico. <see cref="None"/> é de quem ainda não foi
/// convidado (o Identity nunca a devolve: ela nasce no módulo Condomínio, onde a pessoa ainda não tem conta).
/// </summary>
public enum ResidentAccessStatus
{
    None = 0,

    /// <summary>Convite enviado: a conta existe, mas a pessoa ainda não escolheu a senha.</summary>
    Invited = 1,

    /// <summary>A pessoa escolheu a senha e pode entrar.</summary>
    Active = 2,

    /// <summary>A conta foi desativada, suspensa ou excluída: a pessoa não entra.</summary>
    Inactive = 3,
}
