using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Consumption;

/// <summary>
/// Monta a tarifa com as regras do Domain (faixas, gás, percentual) e a guarda. Um reajuste é uma tarifa nova com outro mês
/// de início; repetir o mês corrige a que já existe. Os meses já fechados não mudam: o fechamento guarda os valores que cobrou.
/// </summary>
public sealed class SaveConsumptionTariffCommandHandler(IConsumptionTariffs tariffs, ITenantContext tenant, IClock clock)
    : IRequestHandler<SaveConsumptionTariffCommand, Result<ConsumptionTariffView>>
{
    public async Task<Result<ConsumptionTariffView>> Handle(SaveConsumptionTariffCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ConsumptionTariffView>(FinanceErrors.CondominiumNotFound);
        }

        Result<ConsumptionTariff> tariff = Build(condominiumId, request);

        if (tariff.IsFailure)
        {
            return Result.Failure<ConsumptionTariffView>(tariff.Error);
        }

        Result saved = await tariffs.SaveAsync(tariff.Value, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ConsumptionTariffViews.From(tariff.Value))
            : Result.Failure<ConsumptionTariffView>(saved.Error);
    }

    private Result<ConsumptionTariff> Build(Guid condominiumId, SaveConsumptionTariffCommand request)
    {
        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<ConsumptionTariff>(month.Error);
        }

        Result<TariffScale> water = Scale(request.Water);

        if (water.IsFailure)
        {
            return Result.Failure<ConsumptionTariff>(water.Error);
        }

        Result<TariffScale?> sewage = OptionalScale(request.Sewage);

        if (sewage.IsFailure)
        {
            return Result.Failure<ConsumptionTariff>(sewage.Error);
        }

        Result<GasTariff?> gas = OptionalGas(request.Gas);

        return gas.IsFailure
            ? Result.Failure<ConsumptionTariff>(gas.Error)
            : ConsumptionTariff.Create(condominiumId, month.Value, water.Value, sewage.Value, request.SewagePercent, gas.Value, clock.UtcNow);
    }

    private static Result<TariffScale> Scale(IReadOnlyList<TariffBandInput>? bands) =>
        bands is null
            ? Result.Failure<TariffScale>(ConsumptionErrors.BandsInvalid)
            : TariffScale.Create([.. bands.Select(band => new TariffBand(band.UpToCubicMeters, band.FixedAmount, band.PricePerCubicMeter))]);

    private static Result<TariffScale?> OptionalScale(IReadOnlyList<TariffBandInput>? bands)
    {
        if (bands is null or { Count: 0 })
        {
            return Result.Success<TariffScale?>(null);
        }

        Result<TariffScale> scale = Scale(bands);

        return scale.IsSuccess ? Result.Success<TariffScale?>(scale.Value) : Result.Failure<TariffScale?>(scale.Error);
    }

    private static Result<GasTariff?> OptionalGas(GasTariffInput? input)
    {
        if (input is null)
        {
            return Result.Success<GasTariff?>(null);
        }

        Result<GasTariff> gas = GasTariff.Create(input.KilosPerCubicMeter, input.PricePerKilo);

        return gas.IsSuccess ? Result.Success<GasTariff?>(gas.Value) : Result.Failure<GasTariff?>(gas.Error);
    }
}
