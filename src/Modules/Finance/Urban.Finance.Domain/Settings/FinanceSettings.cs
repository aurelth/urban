using Urban.Finance.Domain.Consumption;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Settings;

/// <summary>
/// As escolhas do condomínio que valem para o fechamento do mês e não pertencem a um lançamento: por ora, o percentual do
/// fundo de reserva. Há uma linha por condomínio; sem ela, o percentual é 0 e o recibo não tem fundo de reserva.
/// </summary>
public sealed class FinanceSettings
{
    private FinanceSettings(Guid condominiumId, decimal reserveFundPercent, DateTimeOffset updatedAt)
    {
        CondominiumId = condominiumId;
        ReserveFundPercent = reserveFundPercent;
        UpdatedAt = updatedAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private FinanceSettings()
    {
    }

    public Guid CondominiumId { get; }

    public decimal ReserveFundPercent { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<FinanceSettings> Create(Guid condominiumId, decimal reserveFundPercent, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<FinanceSettings>(ConsumptionErrors.CondominiumRequired);
        }

        return ReserveFund.IsValidPercent(reserveFundPercent)
            ? Result.Success(new FinanceSettings(condominiumId, reserveFundPercent, now))
            : Result.Failure<FinanceSettings>(ConsumptionErrors.ReserveFundPercentInvalid);
    }

    public Result ChangeReserveFundPercent(decimal reserveFundPercent, DateTimeOffset now)
    {
        if (!ReserveFund.IsValidPercent(reserveFundPercent))
        {
            return Result.Failure(ConsumptionErrors.ReserveFundPercentInvalid);
        }

        ReserveFundPercent = reserveFundPercent;
        UpdatedAt = now;

        return Result.Success();
    }
}
