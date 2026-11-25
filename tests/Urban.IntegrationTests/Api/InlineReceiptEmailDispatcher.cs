using Microsoft.Extensions.DependencyInjection;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Infrastructure.Receipts;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// Executa o envio do recibo na hora, como o Hangfire faria: num escopo novo (sem requisição, então o condomínio vem do id que a fila
/// leva) e engolindo a falha do provedor, que a fila trataria como retentativa. Os testes de API leem o e-mail logo depois do pedido.
/// </summary>
public sealed class InlineReceiptEmailDispatcher(IServiceScopeFactory scopes) : IReceiptEmailDispatcher
{
    public async Task EnqueueAsync(Guid condominiumId, Guid paymentId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        SendReceiptEmailJob job = ActivatorUtilities.CreateInstance<SendReceiptEmailJob>(scope.ServiceProvider);

        try
        {
            await job.RunAsync(condominiumId, paymentId, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // O Hangfire tentaria de novo mais tarde.
        }
    }
}
