using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <summary>O que desativar e reativar repetem: achar a unidade conferindo a versão e gravar o resultado.</summary>
internal static class UnitStatus
{
    public static async Task<Result<UnitRecord>> FindForChangeAsync(IUnits units, Guid unitId, string? version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure<UnitRecord>(UnitErrors.VersionRequired);
        }

        UnitRecord? record = await units.FindAsync(unitId, cancellationToken);
        if (record is null)
        {
            return Result.Failure<UnitRecord>(UnitErrors.NotFound);
        }

        return version == record.Version
            ? Result.Success(record)
            : Result.Failure<UnitRecord>(CondominiumErrors.ConcurrencyConflict);
    }

    public static async Task<Result<UnitView>> SaveAsync(IUnits units, UnitRecord record, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        Result<string> saved = await units.SaveAsync(record, entry, cancellationToken);

        return saved.IsSuccess ? Result.Success(UnitView.From(record.Unit, saved.Value)) : Result.Failure<UnitView>(saved.Error);
    }
}
