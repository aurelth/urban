namespace Urban.Finance.Application.Abstractions;

/// <summary>
/// Põe o envio do recibo por e-mail na fila do segundo plano. A fila guarda só os ids (nunca o PDF nem o endereço): quem executa lê
/// tudo de novo e precisa saber de que condomínio é, porque fora de uma requisição não há token com o tenant.
/// </summary>
public interface IReceiptEmailDispatcher
{
    Task EnqueueAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken);
}
