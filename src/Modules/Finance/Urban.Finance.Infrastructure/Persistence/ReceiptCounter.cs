namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>
/// O último número de recibo emitido em cada condomínio. Só a gravação do pagamento a usa, por SQL (<c>insert ... on conflict do
/// update ... returning</c>), dentro da mesma transação do pagamento: a linha trava um número por vez, e se a transação desfaz o
/// número volta junto, então a sequência não tem buracos.
/// </summary>
internal sealed class ReceiptCounter
{
    public Guid CondominiumId { get; init; }

    public int LastNumber { get; init; }
}
