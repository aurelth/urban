using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os pagamentos no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinancePayments(FinanceDbContext db) : IPayments
{
    public async Task<ChargeRef?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken) =>
        await db.UnitCharges
            .AsNoTracking()
            .Where(charge => charge.Id == chargeId)
            .Join(db.MonthClosings, charge => charge.ClosingId, closing => closing.Id, (charge, closing) => new ChargeRef(charge.Id, closing.Id, closing.Month, closing.DueDate, charge.Amount))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListForChargeAsync(Guid chargeId, CancellationToken cancellationToken) =>
        await db.Payments
            .AsNoTracking()
            .Where(payment => payment.ChargeId == chargeId)
            .OrderByDescending(payment => payment.RegisteredAt)
            .ThenByDescending(payment => payment.Id)
            .ToListAsync(cancellationToken);

    public async Task<Payment?> FindAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await db.Payments.AsNoTracking().SingleOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public async Task<Result> AddAsync(Payment payment, decimal chargeAmount, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        // A cobrança travada faz dois pagamentos simultâneos passarem um de cada vez, cada um vendo o saldo que o outro deixou.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"select 1 from finance.unit_charge where id = {payment.ChargeId} and condominium_id = {payment.CondominiumId} for no key update",
            cancellationToken);

        decimal paid = await db.Payments
            .Where(stored => stored.ChargeId == payment.ChargeId && stored.ReversedAt == null)
            .SumAsync(stored => (decimal?)stored.Amount, cancellationToken) ?? 0m;
        Result fits = ChargeAccount.EnsureFits(chargeAmount, paid, payment.Amount);

        if (fits.IsFailure)
        {
            return fits;
        }

        // O número só é pedido depois de o pagamento caber no saldo: quem é recusado não gasta número. Ele sai na mesma transação
        // do pagamento, então um erro na gravação desfaz os dois e a sequência continua sem buracos.
        payment.IssueReceipt(await NextReceiptNumberAsync(payment.CondominiumId, cancellationToken));
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        if (own is not null)
        {
            await own.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// O próximo número da sequência do condomínio. O <c>insert ... on conflict do update</c> trava a linha do contador até o fim da
    /// transação, então dois pagamentos ao mesmo tempo, mesmo de cobranças diferentes, recebem números diferentes e seguidos.
    /// </summary>
    private async Task<int> NextReceiptNumberAsync(Guid condominiumId, CancellationToken cancellationToken)
    {
        // ToListAsync e não SingleAsync: o EF compõe o SQL em volta do Single (limit 2), e um insert ... returning não se compõe.
        List<int> numbers = await db.Database
            .SqlQuery<int>(
                $"""
                insert into finance.receipt_counter (condominium_id, last_number) values ({condominiumId}, 1)
                on conflict (condominium_id) do update set last_number = finance.receipt_counter.last_number + 1
                returning last_number as "Value"
                """)
            .ToListAsync(cancellationToken);

        return numbers.Single();
    }

    public async Task<Result> ReverseAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Condicionado a ainda não estar estornado: o primeiro de dois estornos simultâneos grava, o segundo não acha linha.
        int rows = await db.Payments
            .Where(stored => stored.Id == payment.Id && stored.ReversedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(stored => stored.ReversedAt, payment.ReversedAt)
                    .SetProperty(stored => stored.ReversedBy, payment.ReversedBy)
                    .SetProperty(stored => stored.ReversalReason, payment.ReversalReason),
                cancellationToken);

        return rows == 1 ? Result.Success() : Result.Failure(PaymentErrors.AlreadyReversed);
    }

    public async Task<bool> AnyForClosingAsync(Guid closingId, CancellationToken cancellationToken) =>
        await db.Payments.AnyAsync(
            payment => db.UnitCharges.Any(charge => charge.Id == payment.ChargeId && charge.ClosingId == closingId), cancellationToken);

    public async Task<MonthPaymentsPage?> ListMonthAsync(
        YearMonth month, ChargeStatus? status, DateOnly today, int page, int pageSize, CancellationToken cancellationToken)
    {
        var closing = await db.MonthClosings
            .AsNoTracking()
            .Where(candidate => candidate.Month == month)
            .Select(candidate => new { candidate.Id, candidate.DueDate })
            .SingleOrDefaultAsync(cancellationToken);

        if (closing is null)
        {
            return null;
        }

        // Uma cobrança por unidade: o mês inteiro cabe numa consulta só, com o pago somado no banco. Filtrar a situação aqui, com
        // ChargeAccount.StatusOf, mantém uma única regra em vez de repeti-la em SQL.
        List<ChargeRow> rows = await db.UnitCharges
            .AsNoTracking()
            .Where(charge => charge.ClosingId == closing.Id)
            .OrderBy(charge => charge.Position)
            .Select(charge => new ChargeRow(
                charge.Id,
                charge.UnitId,
                charge.Block,
                charge.Number,
                charge.Amount,
                db.Payments.Where(payment => payment.ChargeId == charge.Id && payment.ReversedAt == null).Sum(payment => (decimal?)payment.Amount) ?? 0m,
                db.Payments.Where(payment => payment.ChargeId == charge.Id && payment.ReversedAt == null).Max(payment => (DateOnly?)payment.ReceivedOn)))
            .ToListAsync(cancellationToken);

        List<ChargeRow> matching =
            [.. rows.Where(row => status is null || ChargeAccount.StatusOf(row.Amount, row.Paid, closing.DueDate, today) == status)];

        return new MonthPaymentsPage(
            closing.DueDate,
            rows.Sum(row => row.Amount),
            rows.Sum(row => row.Paid),
            matching.Count,
            [.. matching.Skip((page - 1) * pageSize).Take(pageSize)]);
    }
}
