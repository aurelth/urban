using MediatR;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact.ListContactMessages;

public sealed record ListContactMessagesQuery(Guid ActorId, bool UnreadOnly = false, int Page = 1, int PageSize = 20)
    : IRequest<Result<ContactMessageListing>>;
