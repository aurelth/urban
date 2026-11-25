using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>
/// Monta o recibo de um pagamento. Como na fatura, o morador só recebe o de uma unidade em que mora hoje, e de outra unidade a
/// resposta é a mesma de um pagamento que não existe, para o pedido não servir para descobrir pagamentos alheios.
/// </summary>
public sealed class GetReceiptQueryHandler(
    IPayments payments,
    ICharges charges,
    IInvoiceParties parties,
    IResidentHousing housing,
    IReceiptRenderer renderer,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<GetReceiptQuery, Result<ReceiptFile>>
{
    public async Task<Result<ReceiptFile>> Handle(GetReceiptQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ReceiptFile>(FinanceErrors.CondominiumNotFound);
        }

        DateOnly today = FinanceCalendar.Today(clock.UtcNow);

        if (await payments.FindAsync(request.PaymentId, cancellationToken) is not { } payment
            || await charges.FindAsync(payment.ChargeId, cancellationToken) is not { } detail)
        {
            return Result.Failure<ReceiptFile>(PaymentErrors.NotFound);
        }

        if (request.ResidentUserId is { } userId && !await LivesInAsync(userId, detail.Charge.UnitId, today, cancellationToken))
        {
            return Result.Failure<ReceiptFile>(PaymentErrors.NotFound);
        }

        if (await parties.GetCondominiumAsync(cancellationToken) is not { } condominium)
        {
            return Result.Failure<ReceiptFile>(FinanceErrors.CondominiumNotFound);
        }

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await parties.GetResponsiblesAsync([detail.Charge.UnitId], today, cancellationToken);
        decimal paidSoFar = ChargeAccount.Paid(
            (await payments.ListForChargeAsync(payment.ChargeId, cancellationToken)).Where(stored => stored.ReceiptNumber <= payment.ReceiptNumber));

        ReceiptDocument document = new(
            ReceiptText.Code(payment.ReceiptNumber),
            condominium.Name,
            condominium.Cnpj,
            condominium.Address,
            InvoiceText.Unit(detail.Charge.Block, detail.Charge.Number),
            responsibles.TryGetValue(detail.Charge.UnitId, out InvoiceResponsible? responsible) ? responsible.Name : null,
            detail.Month,
            InvoiceText.Reference(detail.Month, detail.Charge.Id),
            detail.Charge.Amount,
            payment.Amount,
            payment.ReceivedOn,
            payment.Method,
            paidSoFar,
            ChargeAccount.Balance(detail.Charge.Amount, paidSoFar),
            FinanceCalendar.Today(payment.RegisteredAt),
            payment.ReversedAt is { } reversedAt ? FinanceCalendar.Today(reversedAt) : null,
            condominium.Manager);

        return Result.Success(new ReceiptFile(ReceiptText.FileName(payment.ReceiptNumber), renderer.Render(document)));
    }

    private async Task<bool> LivesInAsync(Guid userId, Guid unitId, DateOnly today, CancellationToken cancellationToken) =>
        (await housing.GetAsync(userId, today, cancellationToken)).Units.Any(unit => unit.UnitId == unitId);
}
