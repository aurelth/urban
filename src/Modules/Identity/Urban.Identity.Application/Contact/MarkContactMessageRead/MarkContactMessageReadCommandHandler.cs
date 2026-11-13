using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Contact.MarkContactMessageRead;

/// <summary>Marcar de novo uma mensagem já lida não é erro e não grava nada: a primeira leitura é a que vale.</summary>
public sealed class MarkContactMessageReadCommandHandler(
    IUserAccounts accounts,
    IContactMessages messages,
    IClock clock) : IRequestHandler<MarkContactMessageReadCommand, Result>
{
    public async Task<Result> Handle(MarkContactMessageReadCommand request, CancellationToken cancellationToken)
    {
        if (!RegistrationReviewers.IsReviewer(await accounts.FindByIdAsync(request.ActorId, cancellationToken)))
        {
            return Result.Failure(ContactErrors.ReadForbidden);
        }

        ContactMessage? message = await messages.FindAsync(request.MessageId, cancellationToken);
        if (message is null)
        {
            return Result.Failure(ContactErrors.MessageNotFound);
        }

        if (message.MarkRead(clock.UtcNow))
        {
            await messages.MarkReadAsync(message, cancellationToken);
        }

        return Result.Success();
    }
}
