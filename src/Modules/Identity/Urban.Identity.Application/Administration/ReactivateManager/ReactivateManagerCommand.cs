using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Administration.ReactivateManager;

public sealed record ReactivateManagerCommand(Guid ActorId, Guid UserId) : IRequest<Result>;
