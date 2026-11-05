using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// Cadastra até 500 unidades de uma vez, tudo ou nada. Primeiro valida cada linha e procura repetições dentro do próprio
/// lote, tudo em memória; só então consulta o banco (identificadores que já existem e soma das frações) e grava o lote
/// inteiro com um único registro de alteração, que guarda só a quantidade.
/// </summary>
public sealed class CreateUnitsBatchCommandHandler(IUnits units, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<CreateUnitsBatchCommand, UnitBatchResult>
{
    public const int MaxUnits = 500;

    public async Task<UnitBatchResult> Handle(CreateUnitsBatchCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return UnitBatchResult.Rejected(CondominiumErrors.NotFound);
        }

        if (request.Units.Count is 0 or > MaxUnits)
        {
            return UnitBatchResult.Rejected(UnitErrors.BatchSizeInvalid);
        }

        DateTimeOffset now = clock.UtcNow;
        (List<CondominiumUnit> created, List<(int Position, Error Error)> invalid) = Build(condominiumId, request.Units, now);
        if (invalid.Count > 0)
        {
            return Rejected(invalid);
        }

        List<(int Position, Error Error)> repeated = RepeatedInsideTheBatch(created);
        if (repeated.Count > 0)
        {
            return Rejected(repeated);
        }

        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.UnitBatch, [$"count:{created.Count}"], now);
        UnitBatchResult? rejection = null;
        Result atomic = await atomicWork.ExecuteAsync(
            async ct =>
            {
                rejection = await SaveAsync(created, entry, ct);
                return rejection.IsSuccess ? Result.Success() : Result.Failure(rejection.Rejection!);
            },
            cancellationToken);

        return atomic.IsSuccess ? rejection! : UnitBatchResult.Rejected(atomic.Error, rejection?.Issues);
    }

    private static (List<CondominiumUnit> Created, List<(int Position, Error Error)> Invalid) Build(Guid condominiumId, IReadOnlyList<UnitInput> inputs, DateTimeOffset now)
    {
        List<CondominiumUnit> created = [];
        List<(int Position, Error Error)> invalid = [];

        for (int position = 0; position < inputs.Count; position++)
        {
            UnitInput input = inputs[position];
            Result<CondominiumUnit> unit = CondominiumUnit.Create(condominiumId, input.Block, input.Number, input.Type, input.IdealFraction, input.Area, now);
            if (unit.IsSuccess)
            {
                created.Add(unit.Value);
            }
            else
            {
                invalid.Add((position, unit.Error));
            }
        }

        return (created, invalid);
    }

    /// <summary>Toda posição cujo identificador aparece mais de uma vez no lote (ignorando maiúsculas) é marcada, não só a repetida.</summary>
    private static List<(int Position, Error Error)> RepeatedInsideTheBatch(List<CondominiumUnit> created) =>
    [
        .. created
            .Select((unit, position) => (unit.Identity, Position: position))
            .GroupBy(item => item.Identity)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .OrderBy(item => item.Position)
            .Select(item => (item.Position, UnitErrors.IdentifierAlreadyExists)),
    ];

    private async Task<UnitBatchResult> SaveAsync(List<CondominiumUnit> created, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        IReadOnlySet<UnitIdentity> taken = await units.TakenAmongAsync([.. created.Select(u => u.Identity)], cancellationToken);
        List<(int Position, Error Error)> existing =
        [
            .. created
                .Select((unit, position) => (unit, position))
                .Where(item => taken.Contains(item.unit.Identity))
                .Select(item => (item.position, UnitErrors.IdentifierAlreadyExists)),
        ];
        if (existing.Count > 0)
        {
            return Rejected(existing);
        }

        decimal batchFractions = created.Sum(u => u.IdealFraction ?? 0m);
        if (batchFractions > 0m)
        {
            Result fits = await UnitChanges.EnsureFractionFitsAsync(units, null, batchFractions, cancellationToken);
            if (fits.IsFailure)
            {
                return UnitBatchResult.Rejected(fits.Error);
            }
        }

        Result added = await units.AddRangeAsync(created, entry, cancellationToken);

        return added.IsSuccess ? UnitBatchResult.Success(created.Count) : UnitBatchResult.Rejected(added.Error);
    }

    private static UnitBatchResult Rejected(List<(int Position, Error Error)> problems) =>
        UnitBatchResult.Rejected(problems[0].Error, [.. problems.Select(p => new UnitBatchIssue(p.Position, p.Error.Code))]);
}
