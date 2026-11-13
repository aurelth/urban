using MediatR;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact.SendContactReply;

/// <summary>O texto e os arquivos não aparecem no <c>ToString</c>, para nenhum log os revelar.</summary>
public sealed record SendContactReplyCommand(Guid ActorId, Guid MessageId, string? Body, IReadOnlyList<ContactReplyFile> Files)
    : IRequest<Result<ContactReplyReceipt>>
{
    public override string ToString() => nameof(SendContactReplyCommand);
}
