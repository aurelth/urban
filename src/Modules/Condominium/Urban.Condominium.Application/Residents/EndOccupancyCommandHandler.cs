using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Encerra um vínculo do morador. "Hoje" é o de Brasília, porque a data de saída é um dia do calendário do condomínio. A
/// gravação só vale se o vínculo ainda estiver aberto, o que resolve dois pedidos ao mesmo tempo.
/// </summary>
public sealed class EndOccupancyCommandHandler(IResidents residents, ITenantContext tenant, IClock clock, ResidentViews views)
    : IRequestHandler<EndOccupancyCommand, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(EndOccupancyCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ResidentView>(CondominiumErrors.NotFound);
        }

        ResidentRecord? record = await residents.FindAsync(request.ResidentId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<ResidentView>(ResidentErrors.NotFound);
        }

        // O vínculo tem de ser deste morador: o id de outra pessoa é "não encontrado", como se não existisse.
        Occupancy? occupancy = record.Occupancies.Select(item => item.Occupancy).FirstOrDefault(item => item.Id == request.OccupancyId);
        if (occupancy is null)
        {
            return Result.Failure<ResidentView>(OccupancyErrors.NotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        Result movedOut = occupancy.MoveOut(request.EndDate, BrazilCalendar.Today(now));
        if (movedOut.IsFailure)
        {
            return Result.Failure<ResidentView>(movedOut.Error);
        }

        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Resident, ["occupancyEnd"], now, record.Person.Id);
        Result saved = await residents.EndOccupancyAsync(occupancy, entry, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(await views.ViewAsync(record, cancellationToken))
            : Result.Failure<ResidentView>(saved.Error);
    }
}
