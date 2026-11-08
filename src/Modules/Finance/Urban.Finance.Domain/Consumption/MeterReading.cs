using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Consumption;

/// <summary>
/// A leitura do medidor de uma unidade num mês (competência): o que o medidor marcava no mês anterior e o que marca agora,
/// em m³ com até 3 casas (a planilha guarda litros; 1.569.562 litros são 1.569,562 m³). O consumo é a diferença.
/// </summary>
public sealed class MeterReading
{
    public const decimal MaximumReading = 9_999_999.999m;

    private MeterReading(Guid condominiumId, Guid unitId, YearMonth month, ConsumptionService service, decimal previous, decimal current, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        UnitId = unitId;
        Month = month;
        Service = service;
        Previous = previous;
        Current = current;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private MeterReading()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid UnitId { get; }

    public YearMonth Month { get; }

    public ConsumptionService Service { get; }

    public decimal Previous { get; private set; }

    public decimal Current { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public decimal Consumption => Current - Previous;

    public static Result<MeterReading> Create(Guid condominiumId, Guid unitId, YearMonth month, ConsumptionService service, decimal previous, decimal current, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<MeterReading>(ConsumptionErrors.CondominiumRequired);
        }

        if (unitId == Guid.Empty)
        {
            return Result.Failure<MeterReading>(ConsumptionErrors.UnitRequired);
        }

        return AreValid(previous, current)
            ? Result.Success(new MeterReading(condominiumId, unitId, month, service, previous, current, now))
            : Result.Failure<MeterReading>(ConsumptionErrors.ReadingInvalid);
    }

    public Result Correct(decimal previous, decimal current)
    {
        if (!AreValid(previous, current))
        {
            return Result.Failure(ConsumptionErrors.ReadingInvalid);
        }

        Previous = previous;
        Current = current;
        return Result.Success();
    }

    private static bool AreValid(decimal previous, decimal current) =>
        previous >= 0
        && current >= previous
        && current <= MaximumReading
        && MoneyRules.HasAtMostDecimals(previous, 3)
        && MoneyRules.HasAtMostDecimals(current, 3);
}
