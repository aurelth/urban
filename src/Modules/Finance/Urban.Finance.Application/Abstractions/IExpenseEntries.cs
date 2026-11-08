using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência dos lançamentos mensais, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IExpenseEntries
{
    Task<IReadOnlyList<ExpenseEntryRecord>> ListAsync(YearMonth month, CancellationToken cancellationToken);

    /// <summary>Os lançamentos de uma despesa a partir de um mês (inclusive).</summary>
    Task<IReadOnlyList<ExpenseEntryRecord>> ListForExpenseFromAsync(Guid expenseId, YearMonth from, CancellationToken cancellationToken);

    Task<ExpenseEntryRecord?> FindAsync(Guid entryId, CancellationToken cancellationToken);

    /// <summary>Grava os lançamentos juntos. Um lançamento repetido (mesma despesa e mesmo mês) vira <see cref="Urban.Finance.Domain.FinanceErrors.ConcurrencyConflict"/>.</summary>
    Task<Result> AddRangeAsync(IReadOnlyList<ExpenseEntry> entries, CancellationToken cancellationToken);

    /// <returns>A nova versão, ou o conflito de versão.</returns>
    Task<Result<string>> SaveAsync(ExpenseEntryRecord record, CancellationToken cancellationToken);
}
