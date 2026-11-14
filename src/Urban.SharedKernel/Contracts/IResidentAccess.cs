namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para o módulo Condomínio dar acesso ao sistema a um morador já cadastrado. Criar a conta e pedir o
/// e-mail do convite são dois passos de propósito: a conta é gravada na mesma transação em que o Condomínio a liga à pessoa, e o
/// e-mail só é pedido depois do commit, porque a tarefa em segundo plano pode rodar antes dele e não acharia a conta.
/// </summary>
public interface IResidentAccess
{
    /// <summary>Cria a conta do morador, pendente de confirmação: sem senha e sem e-mail enviado.</summary>
    /// <param name="condominiumId">Condomínio do morador, o mesmo do síndico que convida.</param>
    /// <param name="email">E-mail do cadastro do morador; o Identity valida.</param>
    /// <param name="invitedBy">Conta do síndico (vem do token); o Identity confere de novo se ela pode convidar neste condomínio.</param>
    /// <returns>O id da conta nova, ou a falha de negócio: quem convida não pode, e-mail inválido ou que já tem conta.</returns>
    Task<Result<Guid>> CreateAsync(Guid condominiumId, string email, Guid invitedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Pede, em segundo plano, o e-mail com o link para o morador definir a senha. Serve ao primeiro convite e ao reenvio; só vale
    /// para conta que ainda não criou a senha e, no máximo, uma vez por minuto.
    /// </summary>
    Task<Result> SendInvitationAsync(Guid userId, Guid condominiumId, Guid invitedBy, CancellationToken cancellationToken);

    /// <summary>
    /// A situação do acesso de várias contas de uma vez, numa única consulta: a lista de moradores pergunta uma vez por página, nunca
    /// uma por morador. Só entram no resultado as contas de morador deste condomínio; as demais são omitidas.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ResidentAccessStatus>> GetStatusesAsync(Guid condominiumId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>
    /// Corrige o e-mail de um morador cujo convite ainda não foi aceito (a conta não tem senha). Os links abertos da conta deixam de
    /// valer, então quem lê o endereço errado não consegue mais usar o link; o síndico reenvia o convite ao endereço novo. Conta que já
    /// tem senha falha: dali em diante o e-mail é o do login.
    /// </summary>
    Task<Result> ChangeInvitedEmailAsync(Guid userId, Guid condominiumId, string newEmail, Guid changedBy, CancellationToken cancellationToken);
}
