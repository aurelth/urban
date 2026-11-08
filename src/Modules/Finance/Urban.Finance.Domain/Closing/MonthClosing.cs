using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Domain.Closing;

/// <summary>
/// O fechamento de um mês: congela o total e a cobrança de cada unidade. Enquanto existir, os lançamentos do mês não
/// mudam; reabrir é apagar o fechamento (e as cobranças) e fechar de novo.
/// </summary>
public sealed class MonthClosing
{
    private const string DefaultExpensesLabel = "Despesas";
    private const string ReserveFundLabel = "Fundo de reserva";

    private readonly List<UnitCharge> _charges = [];

    private MonthClosing(Guid condominiumId, YearMonth month, DateOnly dueDate, decimal total, ApportionmentBasisKind basisKind, Guid closedBy, DateTimeOffset closedAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        Month = month;
        DueDate = dueDate;
        Total = total;
        BasisKind = basisKind;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private MonthClosing()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public YearMonth Month { get; }

    /// <summary>Vencimento de todas as cobranças deste fechamento, escolhido por quem fechou.</summary>
    public DateOnly DueDate { get; }

    /// <summary>Soma dos lançamentos não cancelados na hora do fechamento.</summary>
    public decimal Total { get; }

    public ApportionmentBasisKind BasisKind { get; }

    public Guid ClosedBy { get; }

    public DateTimeOffset ClosedAt { get; }

    public IReadOnlyList<UnitCharge> Charges => _charges;

    /// <summary>
    /// Fecha o mês: confere os lançamentos, soma o total e monta o recibo de cada unidade. Cada grupo de despesa e o fundo de reserva
    /// são repartidos pelo peso das unidades; água, esgoto e gás vêm da leitura de cada uma. O total do mês continua sendo só o das despesas.
    /// </summary>
    public static Result<MonthClosing> Close(
        Guid condominiumId,
        YearMonth month,
        DateOnly dueDate,
        Guid closedBy,
        DateTimeOffset now,
        IReadOnlyList<ExpenseEntry> entries,
        ApportionmentBasis basis,
        ClosingOptions? options = null)
    {
        if (entries.Count == 0)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.MonthNotOpen);
        }

        List<ExpenseEntry> counted = [.. entries.Where(entry => entry.Status != EntryStatus.Canceled)];

        if (counted.Any(entry => entry.NeedsAmount))
        {
            return Result.Failure<MonthClosing>(ClosingErrors.AmountsPending);
        }

        decimal total = counted.Sum(entry => entry.Amount ?? 0m);

        if (total <= 0)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.NothingToCharge);
        }

        if (dueDate < month.FirstDay)
        {
            return Result.Failure<MonthClosing>(ClosingErrors.DueDateInvalid);
        }

        Error? pending = CheckReadings(month, basis, options);

        if (pending is not null)
        {
            return Result.Failure<MonthClosing>(pending);
        }

        decimal[] weights = [.. basis.Units.Select(unit => unit.Weight)];
        List<(ChargeLineKind Kind, string Label, decimal Total)> shared = SharedLines(counted, options);
        List<IReadOnlyList<decimal>> splits = [];

        foreach ((ChargeLineKind _, string _, decimal lineTotal) in shared)
        {
            Result<IReadOnlyList<decimal>> shares = ChargeSplitter.Split(lineTotal, weights);

            if (shares.IsFailure)
            {
                return Result.Failure<MonthClosing>(shares.Error);
            }

            splits.Add(shares.Value);
        }

        MonthClosing closing = new(condominiumId, month, dueDate, total, basis.Kind, closedBy, now);

        for (int index = 0; index < basis.Units.Count; index++)
        {
            ApportionmentUnit unit = basis.Units[index];
            UnitCharge charge = new(closing.Id, condominiumId, unit.UnitId, index, unit.Block, unit.Number, unit.Weight);

            for (int line = 0; line < shared.Count; line++)
            {
                charge.AddLine(shared[line].Kind, shared[line].Label, splits[line][index]);
            }

            AddConsumptionLines(charge, month, options);
            closing._charges.Add(charge);
        }

        return Result.Success(closing);
    }

    private static Error? CheckReadings(YearMonth month, ApportionmentBasis basis, ClosingOptions? options)
    {
        if (options?.Tariff is not { } tariff)
        {
            return null;
        }

        int Missing(ConsumptionService service) =>
            basis.Units.Count(unit => !options.Readings.Any(reading => reading.UnitId == unit.UnitId && reading.Month == month && reading.Service == service));

        int water = Missing(ConsumptionService.Water);
        int gas = tariff.Gas is null ? 0 : Missing(ConsumptionService.Gas);

        return water + gas > 0 ? ClosingErrors.ReadingsPending(water, gas) : null;
    }

    /// <summary>As linhas repartidas por peso: uma por grupo de despesa e, depois, o fundo de reserva. Linhas sem valor não entram.</summary>
    private static List<(ChargeLineKind Kind, string Label, decimal Total)> SharedLines(List<ExpenseEntry> counted, ClosingOptions? options)
    {
        List<(ChargeLineKind, string, decimal)> lines = [];

        foreach (IGrouping<Guid, ExpenseEntry> group in counted.GroupBy(entry => entry.GroupId))
        {
            decimal groupTotal = group.Sum(entry => entry.Amount ?? 0m);

            if (groupTotal > 0)
            {
                lines.Add((ChargeLineKind.Expenses, LabelOf(group.Key, options), groupTotal));
            }
        }

        if (options is { ReserveFundPercent: > 0 })
        {
            decimal baseTotal = counted.Where(entry => options.ReserveFundGroupIds.Contains(entry.GroupId)).Sum(entry => entry.Amount ?? 0m);
            decimal reserve = ReserveFund.AmountFor(baseTotal, options.ReserveFundPercent);

            if (reserve > 0)
            {
                lines.Add((ChargeLineKind.ReserveFund, ReserveFundLabel, reserve));
            }
        }

        return lines;
    }

    private static string LabelOf(Guid groupId, ClosingOptions? options) =>
        options is not null && options.GroupNames.TryGetValue(groupId, out string? name) ? name : DefaultExpensesLabel;

    private static void AddConsumptionLines(UnitCharge charge, YearMonth month, ClosingOptions? options)
    {
        if (options?.Tariff is not { } tariff)
        {
            return;
        }

        MeterReading? Find(ConsumptionService service) =>
            options.Readings.FirstOrDefault(reading => reading.UnitId == charge.UnitId && reading.Month == month && reading.Service == service);

        if (Find(ConsumptionService.Water) is { } water)
        {
            AddConsumption(charge, ChargeLineKind.Water, "Água", tariff.WaterCharge(water.Consumption), water);
            AddConsumption(charge, ChargeLineKind.Sewage, "Esgoto", tariff.SewageCharge(water.Consumption), water);
        }

        if (Find(ConsumptionService.Gas) is { } gas)
        {
            AddConsumption(charge, ChargeLineKind.Gas, "Gás", tariff.GasCharge(gas.Consumption), gas);
        }
    }

    private static void AddConsumption(UnitCharge charge, ChargeLineKind kind, string label, decimal amount, MeterReading reading)
    {
        if (amount > 0)
        {
            charge.AddLine(kind, label, amount, reading.Consumption, reading.Previous, reading.Current);
        }
    }
}
