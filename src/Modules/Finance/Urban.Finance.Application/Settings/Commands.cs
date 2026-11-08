using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Settings;

public sealed record FinanceSettingsView(decimal ReserveFundPercent);

public sealed record GetFinanceSettingsQuery : IRequest<Result<FinanceSettingsView>>;

public sealed record SetReserveFundPercentCommand(decimal Percent) : IRequest<Result<FinanceSettingsView>>;
