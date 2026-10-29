namespace Urban.SharedKernel.Abstractions;

/// <summary>
/// Executa um trabalho que grava em mais de um módulo como uma transação só: ou tudo é confirmado, ou nada é.
/// Os módulos continuam sem se conhecer; quem junta os bancos deles é a implementação, que mora no projeto que hospeda os módulos.
/// </summary>
public interface IAtomicWork
{
    /// <summary>Confirma se <paramref name="work"/> devolver sucesso; desfaz tudo se devolver falha ou lançar exceção (que sobe).</summary>
    Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken);
}
