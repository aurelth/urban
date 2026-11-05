using Urban.SharedKernel;

namespace Urban.Condominium.Application.Units;

/// <summary>
/// O resultado do lote. Quando é recusado, <see cref="Rejection"/> traz o motivo principal e <see cref="Issues"/> aponta
/// a posição (a partir de 0, na ordem enviada) e o código de cada problema, para a tela marcar as linhas. Em recusas que
/// não são de uma linha (tamanho do lote, soma das frações) a lista vem vazia.
/// </summary>
public sealed record UnitBatchResult(int Created, Error? Rejection, IReadOnlyList<UnitBatchIssue> Issues)
{
    public bool IsSuccess => Rejection is null;

    public static UnitBatchResult Success(int created) => new(created, null, []);

    public static UnitBatchResult Rejected(Error rejection, IReadOnlyList<UnitBatchIssue>? issues = null) => new(0, rejection, issues ?? []);
}

public sealed record UnitBatchIssue(int Position, string Code);
