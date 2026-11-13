using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Contact.NotifyContactMessage;

/// <summary>
/// Roda em segundo plano: avisa o dono de uma mensagem nova. Se ela já foi lida quando a tarefa roda (a fila pode atrasar ou
/// tentar de novo), não há mais o que avisar. Se o envio falha, o erro sobe para o mecanismo de retentativa.
/// </summary>
public sealed class NotifyContactMessageCommandHandler(
    IContactMessages messages,
    IUserAccounts accounts,
    ICondominiumDirectory condominiums,
    IContactMessageNotifier notifier) : IRequestHandler<NotifyContactMessageCommand, Result>
{
    public async Task<Result> Handle(NotifyContactMessageCommand request, CancellationToken cancellationToken)
    {
        ContactMessage? message = await messages.FindAsync(request.MessageId, cancellationToken);
        if (message is null || message.IsRead)
        {
            return Result.Success();
        }

        User? sender = await accounts.FindByIdAsync(message.SenderId, cancellationToken);
        if (sender is null)
        {
            return Result.Success();
        }

        IReadOnlyDictionary<Guid, string> names = await ContactCondominiumNames.ResolveAsync(condominiums, [message.CondominiumId], cancellationToken);
        string? condominiumName = ContactCondominiumNames.NameOf(names, message.CondominiumId);

        await notifier.NotifyAsync(ContactMessageSummary.From(message, sender, condominiumName), cancellationToken);

        return Result.Success();
    }
}
