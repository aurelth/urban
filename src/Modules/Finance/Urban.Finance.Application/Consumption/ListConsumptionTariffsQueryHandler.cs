using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Consumption;

public sealed class ListConsumptionTariffsQueryHandler(IConsumptionTariffs tariffs, ITenantContext tenant)
    : IRequestHandler<ListConsumptionTariffsQuery, Result<IReadOnlyList<ConsumptionTariffView>>>
{
    public async Task<Result<IReadOnlyList<ConsumptionTariffView>>> Handle(ListConsumptionTariffsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<IReadOnlyList<ConsumptionTariffView>>(FinanceErrors.CondominiumNotFound);
        }

        IReadOnlyList<ConsumptionTariff> all = await tariffs.ListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ConsumptionTariffView>>([.. all.Select(ConsumptionTariffViews.From)]);
    }
}
