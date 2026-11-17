namespace Urban.SharedKernel.Abstractions;

/// <summary>
/// Escolhe o condomínio de quem não tem token: uma tarefa em segundo plano. Fora de uma requisição o <see cref="ITenantContext"/> não
/// enxerga nenhum condomínio, e o filtro global do banco esconderia tudo; a tarefa, que recebeu o id do condomínio na fila, o diz
/// aqui antes de qualquer consulta. Só vale no escopo de DI da própria tarefa, e requisição HTTP nenhuma chama isto.
/// </summary>
public interface ITenantScope
{
    void Enter(Guid condominiumId);
}
