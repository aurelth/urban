using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

/// <summary>Grupos em memória. Cada gravação sobe a versão em 1, como o xmin do banco faria.</summary>
internal sealed class FakeExpenseGroups : IExpenseGroups
{
    private readonly Dictionary<Guid, Stored> _store = [];

    public Action? BeforeAdd { get; set; }

    public IReadOnlyList<ExpenseGroup> All => [.. _store.Values.Select(stored => stored.Group)];

    public ExpenseGroup Existing(ExpenseGroup group, int version = 5)
    {
        _store[group.Id] = new Stored(group, version);

        return group;
    }

    public Task<IReadOnlyList<ExpenseGroupRecord>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseGroupRecord>>(
            [.. _store.Values.OrderBy(stored => stored.Group.Name, StringComparer.OrdinalIgnoreCase).Select(Record)]);

    public Task<ExpenseGroupRecord?> FindAsync(Guid groupId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(groupId, out Stored? stored) ? Record(stored) : null);

    public Task<bool> IsNameTakenAsync(string name, Guid? exceptGroupId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Values.Any(stored => stored.Group.Id != exceptGroupId && Same(stored.Group.Name, name)));

    public Task<Result<string>> AddAsync(ExpenseGroup group, CancellationToken cancellationToken)
    {
        BeforeAdd?.Invoke();

        if (_store.Values.Any(stored => Same(stored.Group.Name, group.Name)))
        {
            return Task.FromResult(Result.Failure<string>(ExpenseGroupErrors.NameAlreadyExists));
        }

        _store[group.Id] = new Stored(group, 1);

        return Task.FromResult(Result.Success("1"));
    }

    public Task<Result<string>> SaveAsync(ExpenseGroupRecord record, CancellationToken cancellationToken)
    {
        if (_store.Values.Any(stored => stored.Group.Id != record.Group.Id && Same(stored.Group.Name, record.Group.Name)))
        {
            return Task.FromResult(Result.Failure<string>(ExpenseGroupErrors.NameAlreadyExists));
        }

        Stored current = _store[record.Group.Id];
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ExpenseGroupRecord Record(Stored stored) => new(stored.Group, stored.Version.ToString());

    private sealed class Stored(ExpenseGroup group, int version)
    {
        public ExpenseGroup Group { get; } = group;

        public int Version { get; set; } = version;
    }
}

internal sealed class FakeExpenses : IExpenses
{
    private readonly Dictionary<Guid, Stored> _store = [];

    public IReadOnlyList<Expense> All => [.. _store.Values.Select(stored => stored.Expense)];

    public List<ExpenseRecord> Saved { get; } = [];

    public List<ExpenseRecord> Removed { get; } = [];

    public Error? AddFailure { get; set; }

    public Error? RemoveFailure { get; set; }

    public Expense Existing(Expense expense, int version = 5)
    {
        _store[expense.Id] = new Stored(expense, version);

        return expense;
    }

    public Task<IReadOnlyList<ExpenseRecord>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseRecord>>(
            [.. _store.Values.OrderBy(stored => stored.Expense.Description, StringComparer.OrdinalIgnoreCase).Select(Record)]);

    public Task<ExpenseRecord?> FindAsync(Guid expenseId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(expenseId, out Stored? stored) ? Record(stored) : null);

    public Task<Result<string>> AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        if (AddFailure is { } failure)
        {
            return Task.FromResult(Result.Failure<string>(failure));
        }

        _store[expense.Id] = new Stored(expense, 1);

        return Task.FromResult(Result.Success("1"));
    }

    public Task<Result<string>> SaveAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        Saved.Add(record);
        Stored current = _store[record.Expense.Id];
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    public Task<Result> RemoveAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        if (RemoveFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        _store.Remove(record.Expense.Id);
        Removed.Add(record);

        return Task.FromResult(Result.Success());
    }

    private static ExpenseRecord Record(Stored stored) => new(stored.Expense, stored.Version.ToString());

    private sealed class Stored(Expense expense, int version)
    {
        public Expense Expense { get; } = expense;

        public int Version { get; set; } = version;
    }
}

internal sealed class FakeExpenseEntries : IExpenseEntries
{
    private readonly List<Stored> _store = [];

    public IReadOnlyList<ExpenseEntry> All => [.. _store.Select(stored => stored.Entry)];

    public List<ExpenseEntry> Added { get; } = [];

    public List<ExpenseEntry> Removed { get; } = [];

    public Error? AddRangeFailure { get; set; }

    public ExpenseEntry Existing(ExpenseEntry entry, int version = 5)
    {
        _store.Add(new Stored(entry, version));

        return entry;
    }

    public Task<IReadOnlyList<ExpenseEntryRecord>> ListAsync(YearMonth month, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseEntryRecord>>([.. _store.Where(stored => stored.Entry.Month == month).Select(Record)]);

    public Task<IReadOnlyList<ExpenseEntryRecord>> ListForExpenseFromAsync(Guid expenseId, YearMonth from, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseEntryRecord>>(
            [.. _store.Where(stored => stored.Entry.ExpenseId == expenseId && stored.Entry.Month >= from).Select(Record)]);

    public Task<ExpenseEntryRecord?> FindAsync(Guid entryId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Find(stored => stored.Entry.Id == entryId) is { } stored ? Record(stored) : null);

    public Task<Result> AddRangeAsync(IReadOnlyList<ExpenseEntry> entries, CancellationToken cancellationToken)
    {
        if (AddRangeFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        bool duplicated = entries.Any(entry => _store.Any(stored => stored.Entry.ExpenseId == entry.ExpenseId && stored.Entry.Month == entry.Month));

        if (duplicated)
        {
            return Task.FromResult(Result.Failure(FinanceErrors.ConcurrencyConflict));
        }

        foreach (ExpenseEntry entry in entries)
        {
            _store.Add(new Stored(entry, 1));
            Added.Add(entry);
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<string>> SaveAsync(ExpenseEntryRecord record, CancellationToken cancellationToken)
    {
        Stored current = _store.Single(stored => stored.Entry.Id == record.Entry.Id);
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    public Task<Result> RemoveRangeAsync(IReadOnlyList<ExpenseEntryRecord> records, CancellationToken cancellationToken)
    {
        foreach (ExpenseEntryRecord record in records)
        {
            _store.RemoveAll(stored => stored.Entry.Id == record.Entry.Id);
            Removed.Add(record.Entry);
        }

        return Task.FromResult(Result.Success());
    }

    private static ExpenseEntryRecord Record(Stored stored) => new(stored.Entry, stored.Version.ToString());

    private sealed class Stored(ExpenseEntry entry, int version)
    {
        public ExpenseEntry Entry { get; } = entry;

        public int Version { get; set; } = version;
    }
}

/// <summary>Fechamentos em memória. Um mês só pode ter um; repetir devolve o erro que o índice único do banco daria.</summary>
internal sealed class FakeMonthClosings : IMonthClosings
{
    private readonly List<MonthClosing> _store = [];

    public IReadOnlyList<MonthClosing> All => _store;

    public List<MonthClosing> Added { get; } = [];

    public List<MonthClosing> Removed { get; } = [];

    public MonthClosing Existing(MonthClosing closing)
    {
        _store.Add(closing);

        return closing;
    }

    public Task<MonthClosing?> FindAsync(YearMonth month, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Find(closing => closing.Month == month));

    public Task<IReadOnlySet<YearMonth>> ClosedMonthsFromAsync(YearMonth from, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<YearMonth>>(_store.Where(closing => closing.Month >= from).Select(closing => closing.Month).ToHashSet());

    public Task<Result> AddAsync(MonthClosing closing, CancellationToken cancellationToken)
    {
        if (_store.Any(stored => stored.Month == closing.Month))
        {
            return Task.FromResult(Result.Failure(ClosingErrors.AlreadyClosed));
        }

        _store.Add(closing);
        Added.Add(closing);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> RemoveAsync(MonthClosing closing, CancellationToken cancellationToken)
    {
        _store.Remove(closing);
        Removed.Add(closing);

        return Task.FromResult(Result.Success());
    }
}

/// <summary>O contrato do Condomínio, com o que o teste mandar: por padrão, duas unidades de pesos 0,6 e 0,4.</summary>
internal sealed class FakeApportionmentBasis : IApportionmentBasis
{
    public static readonly ApportionmentUnit First = new(Guid.CreateVersion7(), null, "101", 0.6m);

    public static readonly ApportionmentUnit Second = new(Guid.CreateVersion7(), "A", "102", 0.4m);

    public Result<ApportionmentBasis> Outcome { get; set; } =
        Result.Success(new ApportionmentBasis(ApportionmentBasisKind.IdealFraction, [First, Second]));

    public int Calls { get; private set; }

    public Task<Result<ApportionmentBasis>> GetAsync(CancellationToken cancellationToken)
    {
        Calls++;

        return Task.FromResult(Outcome);
    }
}

internal sealed class FakeFinanceSettings : IFinanceSettings
{
    public FinanceSettings? Stored { get; private set; }

    public int Saves { get; private set; }

    public Error? SaveFailure { get; set; }

    public FinanceSettings Existing(FinanceSettings settings)
    {
        Stored = settings;

        return settings;
    }

    public Task<FinanceSettings?> FindAsync(CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task<Result> SaveAsync(FinanceSettings settings, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        Stored = settings;
        Saves++;

        return Task.FromResult(Result.Success());
    }
}

internal sealed class FakePaymentInstructions : IPaymentInstructionsStore
{
    public PaymentInstructions? Stored { get; private set; }

    public int Saves { get; private set; }

    public Error? SaveFailure { get; set; }

    public PaymentInstructions Existing(PaymentInstructions instructions)
    {
        Stored = instructions;

        return instructions;
    }

    public Task<PaymentInstructions?> FindAsync(CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task<Result> SaveAsync(PaymentInstructions instructions, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        Stored = instructions;
        Saves++;

        return Task.FromResult(Result.Success());
    }
}

/// <summary>Tarifas em memória: guardar uma tarifa que começa no mesmo mês substitui a anterior, como o banco faz.</summary>
internal sealed class FakeConsumptionTariffs : IConsumptionTariffs
{
    private readonly List<ConsumptionTariff> _items = [];

    public IReadOnlyList<ConsumptionTariff> All => _items;

    public Error? SaveFailure { get; set; }

    public ConsumptionTariff Existing(ConsumptionTariff tariff)
    {
        _items.Add(tariff);

        return tariff;
    }

    public Task<IReadOnlyList<ConsumptionTariff>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConsumptionTariff>>([.. _items.OrderByDescending(tariff => tariff.ValidFrom)]);

    public Task<ConsumptionTariff?> FindAsync(YearMonth validFrom, CancellationToken cancellationToken) =>
        Task.FromResult(_items.FirstOrDefault(tariff => tariff.ValidFrom == validFrom));

    public Task<Result> SaveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        _items.RemoveAll(item => item.ValidFrom == tariff.ValidFrom);
        _items.Add(tariff);

        return Task.FromResult(Result.Success());
    }

    public Task RemoveAsync(ConsumptionTariff tariff, CancellationToken cancellationToken)
    {
        _items.Remove(tariff);

        return Task.CompletedTask;
    }
}

/// <summary>Leituras em memória. Guardar adiciona as novas e apaga as removidas, como a gravação do banco faria numa operação só.</summary>
internal sealed class FakeMeterReadings : IMeterReadings
{
    private readonly List<MeterReading> _items = [];

    public IReadOnlyList<MeterReading> All => _items;

    public int Saves { get; private set; }

    public Error? SaveFailure { get; set; }

    public MeterReading Existing(MeterReading reading)
    {
        _items.Add(reading);

        return reading;
    }

    public Task<IReadOnlyList<MeterReading>> ListAsync(YearMonth month, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MeterReading>>([.. _items.Where(reading => reading.Month == month)]);

    public Task<Result> SaveAsync(IReadOnlyList<MeterReading> added, IReadOnlyList<MeterReading> removed, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        _items.AddRange(added);
        _items.RemoveAll(removed.Contains);
        Saves++;

        return Task.FromResult(Result.Success());
    }
}

/// <summary>Os pagamentos em memória; as cobranças vêm dos fechamentos do <see cref="FakeMonthClosings"/>, como no banco.</summary>
internal sealed class FakePayments(FakeMonthClosings closings) : IPayments
{
    private readonly List<Payment> _store = [];

    public IReadOnlyList<Payment> All => _store;

    public List<Payment> Reversed { get; } = [];

    public Payment Existing(Payment payment)
    {
        _store.Add(payment);

        return payment;
    }

    public Task<ChargeRef?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        foreach (MonthClosing closing in closings.All)
        {
            UnitCharge? charge = closing.Charges.FirstOrDefault(candidate => candidate.Id == chargeId);

            if (charge is not null)
            {
                return Task.FromResult<ChargeRef?>(new ChargeRef(charge.Id, closing.Id, closing.Month, closing.DueDate, charge.Amount));
            }
        }

        return Task.FromResult<ChargeRef?>(null);
    }

    public Task<IReadOnlyList<Payment>> ListForChargeAsync(Guid chargeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. _store.Where(payment => payment.ChargeId == chargeId).Reverse()]);

    public Task<Payment?> FindAsync(Guid paymentId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Find(payment => payment.Id == paymentId));

    public Task<Result> AddAsync(Payment payment, decimal chargeAmount, CancellationToken cancellationToken)
    {
        decimal paid = ChargeAccount.Paid(_store.Where(stored => stored.ChargeId == payment.ChargeId));
        Result fits = ChargeAccount.EnsureFits(chargeAmount, paid, payment.Amount);

        if (fits.IsFailure)
        {
            return Task.FromResult(fits);
        }

        _store.Add(payment);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ReverseAsync(Payment payment, CancellationToken cancellationToken)
    {
        Reversed.Add(payment);

        return Task.FromResult(Result.Success());
    }

    public Task<bool> AnyForClosingAsync(Guid closingId, CancellationToken cancellationToken)
    {
        MonthClosing? closing = closings.All.FirstOrDefault(candidate => candidate.Id == closingId);
        HashSet<Guid> chargeIds = closing is null ? [] : [.. closing.Charges.Select(charge => charge.Id)];

        return Task.FromResult(_store.Any(payment => chargeIds.Contains(payment.ChargeId)));
    }

    public Task<MonthPaymentsPage?> ListMonthAsync(
        YearMonth month, ChargeStatus? status, DateOnly today, int page, int pageSize, CancellationToken cancellationToken)
    {
        MonthClosing? closing = closings.All.FirstOrDefault(candidate => candidate.Month == month);

        if (closing is null)
        {
            return Task.FromResult<MonthPaymentsPage?>(null);
        }

        List<ChargeRow> rows =
        [
            .. closing.Charges.OrderBy(charge => charge.Position).Select(charge =>
            {
                List<Payment> counted = [.. _store.Where(payment => payment.ChargeId == charge.Id && !payment.IsReversed)];

                return new ChargeRow(
                    charge.Id, charge.UnitId, charge.Block, charge.Number, charge.Amount, ChargeAccount.Paid(counted),
                    counted.Count == 0 ? null : counted.Max(payment => payment.ReceivedOn));
            }),
        ];

        List<ChargeRow> matching =
            [.. rows.Where(row => status is null || ChargeAccount.StatusOf(row.Amount, row.Paid, closing.DueDate, today) == status)];

        return Task.FromResult<MonthPaymentsPage?>(new MonthPaymentsPage(
            closing.DueDate, rows.Sum(row => row.Amount), rows.Sum(row => row.Paid), matching.Count,
            [.. matching.Skip((page - 1) * pageSize).Take(pageSize)]));
    }
}

/// <summary>As cobranças vêm dos fechamentos do <see cref="FakeMonthClosings"/>, como no banco.</summary>
internal sealed class FakeCharges(FakeMonthClosings closings, FakePayments payments) : ICharges
{
    public int? AskedLimit { get; private set; }

    public Task<ChargeDetail?> FindAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        foreach (MonthClosing closing in closings.All)
        {
            UnitCharge? charge = closing.Charges.FirstOrDefault(candidate => candidate.Id == chargeId);

            if (charge is not null)
            {
                IReadOnlyList<ClosingLineTotal> lineTotals =
                [
                    .. closing.Charges
                        .SelectMany(candidate => candidate.Lines)
                        .GroupBy(line => (line.Kind, line.Label))
                        .OrderBy(group => group.Min(line => line.Position))
                        .Select(group => new ClosingLineTotal(group.Key.Kind, group.Key.Label, group.Sum(line => line.Amount))),
                ];

                return Task.FromResult<ChargeDetail?>(new ChargeDetail(
                    charge, closing.Month, closing.DueDate, closing.ClosedAt, closing.BasisKind, closing.Charges.Sum(candidate => candidate.Weight), lineTotals));
            }
        }

        return Task.FromResult<ChargeDetail?>(null);
    }

    public Task<IReadOnlyList<EarlierCharge>> ListEarlierOpenAsync(Guid unitId, YearMonth before, int limit, CancellationToken cancellationToken)
    {
        AskedLimit = limit;
        IReadOnlyList<EarlierCharge> found =
        [
            .. closings.All
                .Where(closing => closing.Month < before)
                .SelectMany(closing => closing.Charges
                    .Where(charge => charge.UnitId == unitId)
                    .Select(charge => new EarlierCharge(
                        charge.Id, closing.Month, closing.DueDate, charge.Amount, ChargeAccount.Paid(payments.All.Where(payment => payment.ChargeId == charge.Id)))))
                .Where(charge => charge.Amount - charge.Paid > 0)
                .OrderBy(charge => charge.Month)
                .Take(limit),
        ];

        return Task.FromResult(found);
    }
}

internal sealed class FakeInvoiceParties : IInvoiceParties
{
    public InvoiceCondominium? Condominium { get; set; } =
        new(
            "Residencial Aurora",
            "12.345.678/0001-95",
            "Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000",
            new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"));

    public Dictionary<Guid, InvoiceResponsible> Responsibles { get; } = [];

    public DateOnly? AskedOn { get; private set; }

    public IReadOnlyCollection<Guid> AskedAbout { get; private set; } = [];

    public Task<InvoiceCondominium?> GetCondominiumAsync(CancellationToken cancellationToken) => Task.FromResult(Condominium);

    public Task<IReadOnlyDictionary<Guid, InvoiceResponsible>> GetResponsiblesAsync(
        IReadOnlyCollection<Guid> unitIds, DateOnly today, CancellationToken cancellationToken)
    {
        AskedOn = today;
        AskedAbout = unitIds;

        return Task.FromResult<IReadOnlyDictionary<Guid, InvoiceResponsible>>(
            unitIds.Where(Responsibles.ContainsKey).ToDictionary(id => id, id => Responsibles[id]));
    }
}

internal sealed class FakeInvoiceRenderer : IInvoiceRenderer
{
    public static readonly byte[] Output = [0x25, 0x50, 0x44, 0x46, 0x2D];

    public List<InvoiceDocument> Rendered { get; } = [];

    public byte[] Render(InvoiceDocument document)
    {
        Rendered.Add(document);

        return Output;
    }
}
