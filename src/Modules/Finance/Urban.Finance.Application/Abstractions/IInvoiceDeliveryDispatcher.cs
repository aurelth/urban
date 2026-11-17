namespace Urban.Finance.Application.Abstractions;

/// <summary>
/// Põe o envio da fatura na fila do segundo plano. A fila guarda só os ids (nunca o PDF nem o e-mail): quem executa lê tudo de novo
/// e precisa saber de que condomínio é, porque fora de uma requisição não há token com o tenant.
/// </summary>
public interface IInvoiceDeliveryDispatcher
{
    Task EnqueueAsync(Guid condominiumId, Guid chargeId, CancellationToken cancellationToken);
}
