using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Delinquency;

public sealed class ListDelinquencyQueryHandler : IRequestHandler<ListDelinquencyQuery, Result<DelinquencyView>>
{
    public const int MaxPageSize = 200;

    public ListDelinquencyQueryHandler(IOverdueCharges overdue, ITenantContext tenant, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(overdue);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public Task<Result<DelinquencyView>> Handle(ListDelinquencyQuery request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
