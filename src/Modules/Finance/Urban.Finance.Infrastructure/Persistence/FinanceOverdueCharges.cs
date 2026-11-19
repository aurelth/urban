using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>As cobranças em atraso no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceOverdueCharges : IOverdueCharges
{
    public FinanceOverdueCharges(FinanceDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
    }

    public Task<IReadOnlyList<OverdueRow>> ListAsync(DateOnly today, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
