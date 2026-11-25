using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Os envios em memória, sobre as cobranças dos fechamentos do kit. Guarda o que o handler gravou e o que atualizou.</summary>
internal sealed class FakeInvoiceDeliveries(FakeMonthClosings closings) : IInvoiceDeliveries
{
    private readonly Dictionary<Guid, InvoiceDelivery> _store = [];

    public IReadOnlyCollection<InvoiceDelivery> All => _store.Values;

    public List<InvoiceDelivery> Added { get; } = [];

    public List<InvoiceDelivery> Saved { get; } = [];

    public Error? AddFailure { get; set; }

    public int ListCalls { get; private set; }

    public InvoiceDelivery Existing(UnitCharge charge, InvoiceDeliveryStatus status, DateTimeOffset requestedAt, string? failure = null, DateTimeOffset? sentAt = null)
    {
        InvoiceDelivery delivery = InvoiceDelivery.Queue(charge.CondominiumId, charge.Id, Guid.CreateVersion7(), requestedAt);

        if (status == InvoiceDeliveryStatus.Sent)
        {
            delivery.MarkSent(sentAt ?? requestedAt);
        }
        else if (status == InvoiceDeliveryStatus.Failed)
        {
            delivery.MarkFailed(failure ?? InvoiceDeliveryFailure.NotDelivered, requestedAt);
        }

        _store[charge.Id] = delivery;

        return delivery;
    }

    public Task<ClosingCharge?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        foreach (MonthClosing closing in closings.All)
        {
            UnitCharge? charge = closing.Charges.FirstOrDefault(candidate => candidate.Id == chargeId);

            if (charge is not null)
            {
                return Task.FromResult<ClosingCharge?>(Of(closing, charge));
            }
        }

        return Task.FromResult<ClosingCharge?>(null);
    }

    public Task<IReadOnlyList<ClosingCharge>?> ListChargesAsync(YearMonth month, CancellationToken cancellationToken)
    {
        MonthClosing? closing = closings.All.FirstOrDefault(candidate => candidate.Month == month);

        return Task.FromResult<IReadOnlyList<ClosingCharge>?>(closing is null ? null : [.. closing.Charges.OrderBy(charge => charge.Position).Select(charge => Of(closing, charge))]);
    }

    public Task<InvoiceDelivery?> FindAsync(Guid chargeId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(chargeId));

    public Task<IReadOnlyDictionary<Guid, InvoiceDelivery>> ListAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken cancellationToken)
    {
        ListCalls++;

        return Task.FromResult<IReadOnlyDictionary<Guid, InvoiceDelivery>>(
            chargeIds.Where(_store.ContainsKey).ToDictionary(id => id, id => _store[id]));
    }

    public Task<Result> AddAsync(IReadOnlyCollection<InvoiceDelivery> deliveries, CancellationToken cancellationToken)
    {
        if (AddFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        foreach (InvoiceDelivery delivery in deliveries)
        {
            _store[delivery.ChargeId] = delivery;
            Added.Add(delivery);
        }

        return Task.FromResult(Result.Success());
    }

    public Task SaveAsync(InvoiceDelivery delivery, CancellationToken cancellationToken)
    {
        _store[delivery.ChargeId] = delivery;
        Saved.Add(delivery);

        return Task.CompletedTask;
    }

    private static ClosingCharge Of(MonthClosing closing, UnitCharge charge) =>
        new(charge.Id, charge.UnitId, charge.Block, charge.Number, closing.Month, closing.DueDate, charge.Amount);
}

internal sealed class FakeInvoiceDeliveryDispatcher : IInvoiceDeliveryDispatcher
{
    public List<(Guid CondominiumId, Guid ChargeId)> Enqueued { get; } = [];

    public Task EnqueueAsync(Guid condominiumId, Guid chargeId, CancellationToken cancellationToken)
    {
        Enqueued.Add((condominiumId, chargeId));

        return Task.CompletedTask;
    }
}

internal sealed class FakeReceiptEmailDispatcher : IReceiptEmailDispatcher
{
    public List<(Guid CondominiumId, Guid PaymentId)> Enqueued { get; } = [];

    public Task EnqueueAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken)
    {
        Enqueued.Add((condominiumId, paymentId));

        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Ligado, o envio falha como se o provedor o tivesse recusado.</summary>
    public bool Refuse { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Refuse)
        {
            throw new EmailDeliveryException("O provedor recusou o envio (simulado no teste).");
        }

        Sent.Add(message);

        return Task.CompletedTask;
    }
}
