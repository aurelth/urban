using MediatR;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.MyMonth;

public sealed class GetMyAccessQueryHandler(IResidentHousing housing, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetMyAccessQuery, Result<MyMonthAccess>>
{
    private readonly IResidentHousing _housing = housing;
    private readonly ITenantContext _tenant = tenant;
    private readonly IClock _clock = clock;

    public Task<Result<MyMonthAccess>> Handle(GetMyAccessQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(MyMonthAccess.None));
}
