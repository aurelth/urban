using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Abstractions;

/// <summary>Porta de persistência dos fechamentos de mês, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IMonthClosings
{
    /// <returns>O fechamento do mês com as cobranças, ou nulo quando o mês está aberto.</returns>
    Task<MonthClosing?> FindAsync(YearMonth month, CancellationToken cancellationToken);

    /// <summary>Os meses fechados a partir de um mês (inclusive), para quem altera lançamentos de vários meses de uma vez.</summary>
    Task<IReadOnlySet<YearMonth>> ClosedMonthsFromAsync(YearMonth from, CancellationToken cancellationToken);

    /// <summary>Grava o fechamento e as cobranças juntos. Um segundo fechamento do mesmo mês vira <see cref="Urban.Finance.Domain.Closing.ClosingErrors.AlreadyClosed"/>.</summary>
    Task<Result> AddAsync(MonthClosing closing, CancellationToken cancellationToken);

    /// <summary>Apaga o fechamento e as cobranças dele. Não confirma a transação: quem chama decide, dentro da unidade atômica dele.</summary>
    Task RemoveAsync(MonthClosing closing, CancellationToken cancellationToken);
}
