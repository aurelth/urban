using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact.SendContactMessage;

public sealed record SendContactMessageCommand(Guid SenderId, string? Subject, string? Body) : IRequest<Result<ContactMessageReceipt>>;
