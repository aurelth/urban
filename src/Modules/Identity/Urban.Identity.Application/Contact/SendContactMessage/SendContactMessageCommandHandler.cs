using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Contact.SendContactMessage;

/// <summary>
/// Registra a mensagem do síndico e só então enfileira o aviso ao dono: se a gravação falhar, nenhum aviso sai.
/// A conta é lida de novo (o token sozinho não basta), para que um síndico suspenso não consiga escrever.
/// </summary>
public sealed class SendContactMessageCommandHandler(
    IUserAccounts accounts,
    IContactMessages messages,
    IContactMessageDispatcher dispatcher,
    IClock clock) : IRequestHandler<SendContactMessageCommand, Result<ContactMessageReceipt>>
{
    public async Task<Result<ContactMessageReceipt>> Handle(SendContactMessageCommand request, CancellationToken cancellationToken)
    {
        User? sender = await accounts.FindByIdAsync(request.SenderId, cancellationToken);
        if (sender is not { Role: UserRole.Manager, Status: AccountStatus.Active })
        {
            return Result.Failure<ContactMessageReceipt>(ContactErrors.SendForbidden);
        }

        Result<ContactMessage> created = ContactMessage.Send(sender.Id, sender.CondominiumId, request.Subject, request.Body, clock.UtcNow);
        if (!created.IsSuccess)
        {
            return Result.Failure<ContactMessageReceipt>(created.Error);
        }

        ContactMessage message = created.Value;
        await messages.AddAsync(message, cancellationToken);
        await dispatcher.EnqueueAsync(message.Id, cancellationToken);

        return Result.Success(new ContactMessageReceipt(message.Id, message.CreatedAt));
    }
}
