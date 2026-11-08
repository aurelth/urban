using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Settings;

public sealed class GetFinanceSettingsQueryHandler(IFinanceSettings settings, ITenantContext tenant)
    : IRequestHandler<GetFinanceSettingsQuery, Result<FinanceSettingsView>>
{
    public async Task<Result<FinanceSettingsView>> Handle(GetFinanceSettingsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<FinanceSettingsView>(FinanceErrors.CondominiumNotFound);
        }

        FinanceSettings? stored = await settings.FindAsync(cancellationToken);

        return Result.Success(new FinanceSettingsView(stored?.ReserveFundPercent ?? 0m));
    }
}
