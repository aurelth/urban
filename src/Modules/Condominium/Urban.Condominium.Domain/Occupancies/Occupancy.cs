using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Occupancies;

/// <summary>
/// O vínculo de uma pessoa com uma unidade num período. Nunca se apaga: sair da unidade é preencher a data de saída, e o
/// histórico de quem morou ali fica.
/// </summary>
public sealed class Occupancy
{
    private static readonly DateOnly EarliestStart = new(1900, 1, 1);

    private Occupancy(Guid condominiumId, Guid personId, Guid unitId, OccupancyKind kind, DateOnly startDate, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        PersonId = personId;
        UnitId = unitId;
        Kind = kind;
        StartDate = startDate;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private Occupancy()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid PersonId { get; }

    public Guid UnitId { get; }

    public OccupancyKind Kind { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Nulo enquanto a pessoa ainda mora na unidade.</summary>
    public DateOnly? EndDate { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsActive => EndDate is null;

    /// <param name="today">Data de hoje, recebida de fora para a regra continuar pura e testável.</param>
    public static Result<Occupancy> Start(
        Guid condominiumId,
        Guid personId,
        Guid unitId,
        OccupancyKind kind,
        DateOnly? startDate,
        DateOnly today,
        DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Occupancy>(OccupancyErrors.CondominiumRequired);
        }

        if (personId == Guid.Empty)
        {
            return Result.Failure<Occupancy>(OccupancyErrors.PersonRequired);
        }

        if (unitId == Guid.Empty)
        {
            return Result.Failure<Occupancy>(OccupancyErrors.UnitRequired);
        }

        if (!Enum.IsDefined(kind))
        {
            return Result.Failure<Occupancy>(OccupancyErrors.KindInvalid);
        }

        if (startDate is not { } start || start < EarliestStart || start > today.AddYears(1))
        {
            return Result.Failure<Occupancy>(OccupancyErrors.StartDateInvalid);
        }

        return Result.Success(new Occupancy(condominiumId, personId, unitId, kind, start, now));
    }

    /// <summary>
    /// Encerra o vínculo: a pessoa saiu da unidade. O registro fica, com a data de saída, para o histórico de quem morou ali.
    /// A saída é de entre a data de entrada e hoje.
    /// </summary>
    /// <param name="today">Data de hoje, recebida de fora para a regra continuar pura e testável.</param>
    public Result MoveOut(DateOnly? endDate, DateOnly today)
    {
        if (EndDate is not null)
        {
            return Result.Failure(OccupancyErrors.AlreadyEnded);
        }

        if (endDate is not { } end || end < StartDate || end > today)
        {
            return Result.Failure(OccupancyErrors.EndDateInvalid);
        }

        EndDate = end;

        return Result.Success();
    }
}
