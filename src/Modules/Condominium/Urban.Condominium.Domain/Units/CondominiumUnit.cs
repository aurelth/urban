using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Units;

/// <summary>
/// Uma unidade do condomínio (apartamento ou casa). O nome é <c>CondominiumUnit</c> e não <c>Unit</c> para não se confundir
/// com <c>MediatR.Unit</c>. A unidade nunca é apagada, só desativada, porque morador e cobrança vão apontar para ela.
/// </summary>
public sealed class CondominiumUnit
{
    public const int NumberMaxLength = 20;
    public const int BlockMaxLength = 30;
    public const int FractionDecimalPlaces = 8;
    public const int AreaDecimalPlaces = 2;
    public const decimal AreaMaximum = 99999.99m;

    private CondominiumUnit(Guid condominiumId, UnitData data, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        CreatedAt = createdAt;
        Number = data.Number;
        Apply(data);
    }

    // Usado só pelo EF Core ao ler do banco.
    private CondominiumUnit()
    {
        Number = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    /// <summary>Nulo quando o condomínio não tem blocos.</summary>
    public string? Block { get; private set; }

    public string Number { get; private set; }

    public UnitType Type { get; private set; }

    /// <summary>Guardada de 0 a 1 (1,25% é 0,0125); a tela mostra e recebe em percentual. Opcional: nem todo condomínio rateia por ela.</summary>
    public decimal? IdealFraction { get; private set; }

    /// <summary>Área privativa em m². Opcional.</summary>
    public decimal? Area { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    public UnitIdentity Identity => new(Block, Number);

    public static Result<CondominiumUnit> Create(
        Guid condominiumId,
        string? block,
        string? number,
        UnitType type,
        decimal? idealFraction,
        decimal? area,
        DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<CondominiumUnit>(UnitErrors.CondominiumRequired);
        }

        Result<UnitData> data = Normalize(block, number, type, idealFraction, area);

        return data.IsSuccess
            ? Result.Success(new CondominiumUnit(condominiumId, data.Value, now))
            : Result.Failure<CondominiumUnit>(data.Error);
    }

    /// <summary>
    /// Valida e normaliza os dados sem criar nem tocar em nenhuma unidade: serve a quem precisa saber se a criação ou a
    /// edição vai passar (e o que vai ser gravado) antes de consultar o banco.
    /// </summary>
    public static Result<UnitData> Normalize(string? block, string? number, UnitType type, decimal? idealFraction, decimal? area)
    {
        string normalizedNumber = TextRules.Normalize(number);
        if (normalizedNumber.Length is 0 or > NumberMaxLength || TextRules.HasControlCharacters(normalizedNumber))
        {
            return Result.Failure<UnitData>(UnitErrors.NumberInvalid);
        }

        string normalizedBlock = TextRules.Normalize(block);
        if (normalizedBlock.Length > BlockMaxLength || TextRules.HasControlCharacters(normalizedBlock))
        {
            return Result.Failure<UnitData>(UnitErrors.BlockInvalid);
        }

        if (!Enum.IsDefined(type))
        {
            return Result.Failure<UnitData>(UnitErrors.TypeInvalid);
        }

        if (idealFraction is { } fraction && !IsValidFraction(fraction))
        {
            return Result.Failure<UnitData>(UnitErrors.IdealFractionInvalid);
        }

        if (area is { } squareMeters && !IsValidArea(squareMeters))
        {
            return Result.Failure<UnitData>(UnitErrors.AreaInvalid);
        }

        return Result.Success(new UnitData(normalizedBlock.Length == 0 ? null : normalizedBlock, normalizedNumber, type, idealFraction, area));
    }

    /// <summary>Troca os dados da unidade. Se algum for inválido nada muda. Unidade desativada não se edita: reative primeiro.</summary>
    public Result Update(string? block, string? number, UnitType type, decimal? idealFraction, decimal? area)
    {
        if (!IsActive)
        {
            return Result.Failure(UnitErrors.Deactivated);
        }

        Result<UnitData> data = Normalize(block, number, type, idealFraction, area);
        if (data.IsFailure)
        {
            return Result.Failure(data.Error);
        }

        Apply(data.Value);

        return Result.Success();
    }

    public Result Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return Result.Failure(UnitErrors.AlreadyDeactivated);
        }

        DeactivatedAt = now;

        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(UnitErrors.AlreadyActive);
        }

        DeactivatedAt = null;

        return Result.Success();
    }

    private void Apply(UnitData data)
    {
        Block = data.Block;
        Number = data.Number;
        Type = data.Type;
        IdealFraction = data.IdealFraction;
        Area = data.Area;
    }

    private static bool IsValidFraction(decimal fraction) => fraction is > 0m and <= 1m && decimal.Round(fraction, FractionDecimalPlaces) == fraction;

    private static bool IsValidArea(decimal area) => area is > 0m and <= AreaMaximum && decimal.Round(area, AreaDecimalPlaces) == area;
}
