using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact.NotifyContactMessage;

public sealed record NotifyContactMessageCommand(Guid MessageId) : IRequest<Result>;
