using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Consumption;

public sealed class DeleteConsumptionTariffCommandHandler(IConsumptionTariffs tariffs, ITenantContext tenant)
    : IRequestHandler<DeleteConsumptionTariffCommand, Result>
{
    public async Task<Result> Handle(DeleteConsumptionTariffCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure(month.Error);
        }

        ConsumptionTariff? tariff = await tariffs.FindAsync(month.Value, cancellationToken);

        if (tariff is null)
        {
            return Result.Failure(ConsumptionErrors.TariffNotFound);
        }

        await tariffs.RemoveAsync(tariff, cancellationToken);

        return Result.Success();
    }
}
