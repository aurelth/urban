using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.Units;

/// <summary>O que os handlers de unidades têm em comum: os nomes dos campos do registro, a regra da soma e a execução na unidade atômica.</summary>
internal static class UnitChanges
{
    /// <summary>Nomes dos campos preenchidos ao cadastrar. Número e tipo são sempre obrigatórios.</summary>
    public static List<string> FilledIn(UnitData data)
    {
        List<string> fields = [];
        AddIf(fields, "block", data.Block is not null);
        fields.Add("number");
        fields.Add("type");
        AddIf(fields, "idealFraction", data.IdealFraction is not null);
        AddIf(fields, "area", data.Area is not null);

        return fields;
    }

    /// <summary>Nomes dos campos que mudaram de uma unidade para os dados novos (já normalizados).</summary>
    public static List<string> Changed(CondominiumUnit unit, UnitData data)
    {
        List<string> fields = [];
        AddIf(fields, "block", data.Block != unit.Block);
        AddIf(fields, "number", data.Number != unit.Number);
        AddIf(fields, "type", data.Type != unit.Type);
        AddIf(fields, "idealFraction", data.IdealFraction != unit.IdealFraction);
        AddIf(fields, "area", data.Area != unit.Area);

        return fields;
    }

    /// <summary>Confere a soma das frações com <paramref name="addedFraction"/> a mais. A consulta ignora a unidade informada (a que está sendo editada ou reativada).</summary>
    public static async Task<Result> EnsureFractionFitsAsync(IUnits units, Guid? exceptUnitId, decimal addedFraction, CancellationToken cancellationToken)
    {
        decimal others = await units.ActiveFractionTotalAsync(exceptUnitId, cancellationToken);

        return IdealFractions.EnsureWithinWhole(others + addedFraction);
    }

    public static ChangeLogEntry Entry(Guid condominiumId, Guid actorUserId, IReadOnlyList<string> fields, DateTimeOffset at, Guid? subjectId) =>
        new(condominiumId, actorUserId, ChangeKind.Unit, fields, at, subjectId);

    /// <summary>Roda o trabalho na unidade atômica e devolve o resultado dele; se a unidade atômica falhar por outro motivo, devolve essa falha.</summary>
    public static async Task<Result<T>> InAtomicWorkAsync<T>(IAtomicWork atomicWork, Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        Result<T>? outcome = null;
        Result atomic = await atomicWork.ExecuteAsync(
            async ct =>
            {
                outcome = await work(ct);
                return outcome;
            },
            cancellationToken);

        return atomic.IsSuccess ? outcome! : Result.Failure<T>(atomic.Error);
    }

    private static void AddIf(List<string> fields, string name, bool condition)
    {
        if (condition)
        {
            fields.Add(name);
        }
    }
}
