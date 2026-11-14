using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Invitations.SendResidentInvitation;

public sealed record SendResidentInvitationCommand(Guid UserId) : IRequest<Result>;
