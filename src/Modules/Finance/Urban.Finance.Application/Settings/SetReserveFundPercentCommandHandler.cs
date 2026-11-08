using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Settings;

public sealed class SetReserveFundPercentCommandHandler(IFinanceSettings settings, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetReserveFundPercentCommand, Result<FinanceSettingsView>>
{
    public async Task<Result<FinanceSettingsView>> Handle(SetReserveFundPercentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<FinanceSettingsView>(FinanceErrors.CondominiumNotFound);
        }

        Result<FinanceSettings> prepared = await PrepareAsync(condominiumId, request.Percent, cancellationToken);

        if (prepared.IsFailure)
        {
            return Result.Failure<FinanceSettingsView>(prepared.Error);
        }

        Result saved = await settings.SaveAsync(prepared.Value, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(new FinanceSettingsView(prepared.Value.ReserveFundPercent))
            : Result.Failure<FinanceSettingsView>(saved.Error);
    }

    private async Task<Result<FinanceSettings>> PrepareAsync(Guid condominiumId, decimal percent, CancellationToken cancellationToken)
    {
        FinanceSettings? existing = await settings.FindAsync(cancellationToken);

        if (existing is null)
        {
            return FinanceSettings.Create(condominiumId, percent, clock.UtcNow);
        }

        Result changed = existing.ChangeReserveFundPercent(percent, clock.UtcNow);

        return changed.IsSuccess ? Result.Success(existing) : Result.Failure<FinanceSettings>(changed.Error);
    }
}
