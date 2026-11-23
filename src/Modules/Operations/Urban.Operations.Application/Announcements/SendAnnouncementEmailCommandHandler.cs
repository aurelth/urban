using MediatR;
using Urban.Operations.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>O envio de verdade do comunicado a uma conta, no segundo plano.</summary>
public sealed class SendAnnouncementEmailCommandHandler(
    IAnnouncements announcements,
    IAccountEmails accounts,
    IAnnouncementLinks links,
    IEmailSender email,
    ITenantContext tenant) : IRequestHandler<SendAnnouncementEmailCommand, Result>
{
    public Task<Result> Handle(SendAnnouncementEmailCommand request, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {request.AnnouncementId} {announcements.GetType().Name} {accounts.GetType().Name} {links.GetType().Name} {email.GetType().Name} {tenant.CondominiumId}");
}
