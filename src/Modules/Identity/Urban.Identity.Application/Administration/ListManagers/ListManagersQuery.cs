using MediatR;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration.ListManagers;

public sealed record ListManagersQuery(
    Guid ActorId,
    AccountStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ManagerListing>>;
