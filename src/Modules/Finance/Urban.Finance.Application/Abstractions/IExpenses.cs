using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>
/// Porta de persistência das despesas, sempre dentro do condomínio da requisição (tenant). Um condomínio tem de dezenas a
/// poucas centenas de despesas, então a lista vem inteira e os filtros por situação são feitos em memória.
/// </summary>
public interface IExpenses
{
    /// <summary>Todas as despesas do condomínio, com parcelas e reajustes, em ordem alfabética da descrição.</summary>
    Task<IReadOnlyList<ExpenseRecord>> ListAsync(CancellationToken cancellationToken);

    Task<ExpenseRecord?> FindAsync(Guid expenseId, CancellationToken cancellationToken);

    /// <returns>A versão da nova despesa.</returns>
    Task<Result<string>> AddAsync(Expense expense, CancellationToken cancellationToken);

    /// <summary>Grava as mudanças feitas na despesa lida por <see cref="FindAsync"/>.</summary>
    /// <returns>A nova versão, ou o conflito de versão.</returns>
    Task<Result<string>> SaveAsync(ExpenseRecord record, CancellationToken cancellationToken);
}
