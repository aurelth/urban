using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration.GetPlatformSummary;

public sealed record GetPlatformSummaryQuery(Guid ActorId) : IRequest<Result<PlatformSummary>>;
