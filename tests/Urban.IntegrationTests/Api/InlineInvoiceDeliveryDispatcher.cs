using Microsoft.Extensions.DependencyInjection;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Infrastructure.Invoices;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// Executa o envio da fatura na hora, como o Hangfire faria: num escopo novo (sem requisição, então o condomínio vem do id que a fila
/// leva) e engolindo a falha do provedor, que a fila trataria como retentativa. Os testes de API leem o e-mail logo depois do pedido.
/// </summary>
public sealed class InlineInvoiceDeliveryDispatcher(IServiceScopeFactory scopes) : IInvoiceDeliveryDispatcher
{
    public async Task EnqueueAsync(Guid condominiumId, Guid chargeId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        SendInvoiceJob job = ActivatorUtilities.CreateInstance<SendInvoiceJob>(scope.ServiceProvider);

        try
        {
            await job.RunAsync(condominiumId, chargeId, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // O Hangfire tentaria de novo mais tarde; o envio já ficou gravado como falho.
        }
    }
}
