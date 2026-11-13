using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact.MarkContactMessageRead;

public sealed record MarkContactMessageReadCommand(Guid ActorId, Guid MessageId) : IRequest<Result>;
