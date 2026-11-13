using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration.SuspendManager;

public sealed record SuspendManagerCommand(Guid ActorId, Guid UserId, string? Reason) : IRequest<Result>;
