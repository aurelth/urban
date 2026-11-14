using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Vehicles;

/// <summary>
/// Um veículo de uma unidade do condomínio. Pertence à unidade e não a uma pessoa: mudou o morador, o carro continua na
/// unidade até o síndico mudar ou excluir. A placa é única no condomínio.
/// </summary>
public sealed class Vehicle
{
    public const int ModelMinLength = 2;
    public const int ModelMaxLength = 60;
    public const int ColorMinLength = 2;
    public const int ColorMaxLength = 30;

    private Vehicle(Guid condominiumId, Guid unitId, VehicleData data, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        UnitId = unitId;
        CreatedAt = createdAt;
        Plate = data.Plate;
        Model = data.Model;
        Color = data.Color;
    }

    // Usado só pelo EF Core ao ler do banco.
    private Vehicle()
    {
        Plate = null!;
        Model = null!;
        Color = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid UnitId { get; private set; }

    /// <summary>Normalizada: maiúsculas, sem hífen, nos formatos ABC1234 ou ABC1D23.</summary>
    public string Plate { get; private set; }

    public string Model { get; private set; }

    public string Color { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Result<Vehicle> Create(Guid condominiumId, Guid unitId, string? plate, string? model, string? color, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Vehicle>(VehicleErrors.CondominiumRequired);
        }

        if (unitId == Guid.Empty)
        {
            return Result.Failure<Vehicle>(VehicleErrors.UnitRequired);
        }

        Result<VehicleData> data = Normalize(plate, model, color);

        return data.IsSuccess
            ? Result.Success(new Vehicle(condominiumId, unitId, data.Value, now))
            : Result.Failure<Vehicle>(data.Error);
    }

    /// <summary>
    /// Valida e normaliza os dados sem criar nem tocar em nenhum veículo, na ordem do formulário (placa, modelo, cor): serve a
    /// quem precisa saber se a criação ou a edição vai passar, e o que vai ser gravado, antes de consultar o banco.
    /// </summary>
    public static Result<VehicleData> Normalize(string? plate, string? model, string? color)
    {
        Result<string> normalizedPlate = LicensePlate.Normalize(plate);
        if (normalizedPlate.IsFailure)
        {
            return Result.Failure<VehicleData>(normalizedPlate.Error);
        }

        string normalizedModel = TextRules.Normalize(model);
        if (!IsValidText(normalizedModel, ModelMinLength, ModelMaxLength))
        {
            return Result.Failure<VehicleData>(VehicleErrors.ModelInvalid);
        }

        string normalizedColor = TextRules.Normalize(color);
        if (!IsValidText(normalizedColor, ColorMinLength, ColorMaxLength))
        {
            return Result.Failure<VehicleData>(VehicleErrors.ColorInvalid);
        }

        return Result.Success(new VehicleData(normalizedPlate.Value, normalizedModel, normalizedColor));
    }

    /// <summary>Troca a unidade e os dados do veículo. Se algum for inválido nada muda.</summary>
    public Result Update(Guid unitId, string? plate, string? model, string? color)
    {
        if (unitId == Guid.Empty)
        {
            return Result.Failure(VehicleErrors.UnitRequired);
        }

        Result<VehicleData> data = Normalize(plate, model, color);
        if (data.IsFailure)
        {
            return Result.Failure(data.Error);
        }

        UnitId = unitId;
        Plate = data.Value.Plate;
        Model = data.Value.Model;
        Color = data.Value.Color;

        return Result.Success();
    }

    private static bool IsValidText(string text, int min, int max) =>
        text.Length >= min && text.Length <= max && !TextRules.HasControlCharacters(text);
}
