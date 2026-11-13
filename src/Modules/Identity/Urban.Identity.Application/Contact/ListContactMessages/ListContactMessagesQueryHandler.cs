using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Contact.ListContactMessages;

/// <summary>
/// A caixa de entrada do dono. O Identity só guarda o identificador do condomínio; o nome real vem do módulo Condomínio
/// pelo contrato <see cref="ICondominiumDirectory"/>, numa única consulta para a página inteira.
/// </summary>
public sealed class ListContactMessagesQueryHandler(
    IUserAccounts accounts,
    IContactMessages messages,
    ICondominiumDirectory condominiums) : IRequestHandler<ListContactMessagesQuery, Result<ContactMessageListing>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<ContactMessageListing>> Handle(ListContactMessagesQuery request, CancellationToken cancellationToken)
    {
        if (!RegistrationReviewers.IsReviewer(await accounts.FindByIdAsync(request.ActorId, cancellationToken)))
        {
            return Result.Failure<ContactMessageListing>(ContactErrors.ReadForbidden);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<ContactMessageListing>(ContactErrors.PageInvalid);
        }

        ContactMessagePage page = await messages.ListAsync(request.UnreadOnly, request.Page, request.PageSize, cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await ContactCondominiumNames.ResolveAsync(condominiums, page.Items.Select(e => e.Message.CondominiumId), cancellationToken);

        ContactMessageSummary[] items =
        [
            .. page.Items.Select(e => ContactMessageSummary.From(e.Message, e.Sender, ContactCondominiumNames.NameOf(names, e.Message.CondominiumId))),
        ];

        return Result.Success(new ContactMessageListing(items, page.Total, request.Page, request.PageSize));
    }
}
