using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência dos grupos de despesa, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IExpenseGroups
{
    /// <summary>Todos os grupos do condomínio, em ordem alfabética.</summary>
    Task<IReadOnlyList<ExpenseGroupRecord>> ListAsync(CancellationToken cancellationToken);

    Task<ExpenseGroupRecord?> FindAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Se já existe um grupo com o nome (sem diferenciar maiúsculas), sem contar <paramref name="exceptGroupId"/>.</summary>
    Task<bool> IsNameTakenAsync(string name, Guid? exceptGroupId, CancellationToken cancellationToken);

    /// <returns>A versão do novo grupo, ou <see cref="ExpenseGroupErrors.NameAlreadyExists"/> se outra requisição criou o mesmo nome antes (índice único).</returns>
    Task<Result<string>> AddAsync(ExpenseGroup group, CancellationToken cancellationToken);

    /// <returns>A nova versão, ou a falha: conflito de versão ou nome repetido.</returns>
    Task<Result<string>> SaveAsync(ExpenseGroupRecord record, CancellationToken cancellationToken);
}
