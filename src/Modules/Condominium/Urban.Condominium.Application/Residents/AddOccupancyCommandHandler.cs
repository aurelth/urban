using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Residents;

/// <summary>
/// Liga um morador existente a mais uma unidade. Quem já mora na unidade não ganha um segundo vínculo ativo (a conferência em
/// memória dá a resposta rápida e o índice único do banco resiste a dois pedidos ao mesmo tempo); quem já saiu pode voltar.
/// Depois de gravar, devolve o morador como o banco o lê, já com o vínculo novo.
/// </summary>
public sealed class AddOccupancyCommandHandler(IResidents residents, IUnits units, ITenantContext tenant, IClock clock, ResidentViews views)
    : IRequestHandler<AddOccupancyCommand, Result<ResidentView>>
{
    public async Task<Result<ResidentView>> Handle(AddOccupancyCommand request, CancellationToken cancellationToken)
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

        UnitRecord? unit = await units.FindAsync(request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure<ResidentView>(UnitErrors.NotFound);
        }

        if (!unit.Unit.IsActive)
        {
            return Result.Failure<ResidentView>(UnitErrors.Deactivated);
        }

        if (record.Occupancies.Any(item => item.Occupancy.UnitId == unit.Unit.Id && item.Occupancy.IsActive))
        {
            return Result.Failure<ResidentView>(OccupancyErrors.AlreadyActive);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<Occupancy> occupancy = Occupancy.Start(
            condominiumId, record.Person.Id, unit.Unit.Id, request.Kind, request.StartDate, BrazilCalendar.Today(now), now);
        if (occupancy.IsFailure)
        {
            return Result.Failure<ResidentView>(occupancy.Error);
        }

        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Resident, ["occupancy"], now, record.Person.Id);
        Result added = await residents.AddOccupancyAsync(occupancy.Value, entry, cancellationToken);
        if (added.IsFailure)
        {
            return Result.Failure<ResidentView>(added.Error);
        }

        ResidentRecord? reloaded = await residents.FindAsync(record.Person.Id, cancellationToken);

        return reloaded is null
            ? Result.Failure<ResidentView>(ResidentErrors.NotFound)
            : Result.Success(await views.ViewAsync(reloaded, cancellationToken));
    }
}
