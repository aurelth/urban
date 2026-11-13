using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Identity.Application.Administration.ListManagers;

/// <summary>
/// A lista de síndicos do administrador. O Identity só guarda o identificador do condomínio; o nome real vem do módulo
/// Condomínio pelo contrato <see cref="ICondominiumDirectory"/>, numa única consulta para a página inteira.
/// </summary>
public sealed class ListManagersQueryHandler(
    IUserAccounts accounts,
    IManagerAccounts managers,
    ICondominiumDirectory condominiums) : IRequestHandler<ListManagersQuery, Result<ManagerListing>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<ManagerListing>> Handle(ListManagersQuery request, CancellationToken cancellationToken)
    {
        if (!RegistrationReviewers.IsReviewer(await accounts.FindByIdAsync(request.ActorId, cancellationToken)))
        {
            return Result.Failure<ManagerListing>(AdministrationErrors.Forbidden);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<ManagerListing>(AdministrationErrors.PageInvalid);
        }

        if (request.Status is { } status && !ManagerStatuses.Listed.Contains(status))
        {
            return Result.Failure<ManagerListing>(AdministrationErrors.StatusFilterInvalid);
        }

        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        ManagerPage page = await managers.ListAsync(new ManagerFilter(request.Status, search), request.Page, request.PageSize, cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await NamesOfAsync(page, cancellationToken);

        ManagerSummary[] items =
        [
            .. page.Items.Select(user => ManagerSummary.From(user, user.CondominiumId is { } id && names.TryGetValue(id, out string? name) ? name : null)),
        ];

        return Result.Success(new ManagerListing(items, page.Total, request.Page, request.PageSize));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NamesOfAsync(ManagerPage page, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. page.Items.Where(u => u.CondominiumId is not null).Select(u => u.CondominiumId!.Value).Distinct()];

        return ids.Length == 0 ? new Dictionary<Guid, string>() : await condominiums.GetNamesAsync(ids, cancellationToken);
    }
}
