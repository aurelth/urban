using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

public sealed class GetMyResidentQueryHandler(IResidents residents, ITenantContext tenant, ResidentViews views) : IRequestHandler<GetMyResidentQuery, Result<ResidentView>>
{
    public Task<Result<ResidentView>> Handle(GetMyResidentQuery request, CancellationToken cancellationToken)
    {
        _ = (residents, tenant, views);

        return Task.FromResult(Result.Failure<ResidentView>(ResidentErrors.NotFound));
    }
}
