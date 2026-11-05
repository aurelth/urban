using MediatR;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>Troca o método de rateio do condomínio. Só a escolha é gravada; o cálculo das cobranças mora em outro módulo. Escolher o método que já está valendo não grava nada.</summary>
public sealed class ChangeApportionmentMethodCommandHandler(IApportionments apportionments, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<ChangeApportionmentMethodCommand, Result<ApportionmentView>>
{
    public async Task<Result<ApportionmentView>> Handle(ChangeApportionmentMethodCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ApportionmentView>(CondominiumErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<ApportionmentView>(CondominiumErrors.VersionRequired);
        }

        ApportionmentSetting? setting = await apportionments.FindAsync(condominiumId, cancellationToken);
        if (setting is null)
        {
            return Result.Failure<ApportionmentView>(CondominiumErrors.NotFound);
        }

        if (request.Version != setting.Version)
        {
            return Result.Failure<ApportionmentView>(CondominiumErrors.ConcurrencyConflict);
        }

        if (request.Method == setting.Condominium.ApportionmentMethod)
        {
            return Result.Success(new ApportionmentView(request.Method, setting.Version));
        }

        Result changed = setting.Condominium.ChangeApportionmentMethod(request.Method);
        if (changed.IsFailure)
        {
            return Result.Failure<ApportionmentView>(changed.Error);
        }

        ChangeLogEntry entry = new(condominiumId, request.ActorUserId, ChangeKind.Apportionment, ["apportionmentMethod"], clock.UtcNow);

        return await UnitChanges.InAtomicWorkAsync(
            atomicWork,
            async ct =>
            {
                Result<string> saved = await apportionments.SaveAsync(setting, entry, ct);

                return saved.IsSuccess
                    ? Result.Success(new ApportionmentView(request.Method, saved.Value))
                    : Result.Failure<ApportionmentView>(saved.Error);
            },
            cancellationToken);
    }
}
