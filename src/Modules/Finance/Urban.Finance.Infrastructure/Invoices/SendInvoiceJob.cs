using Hangfire;
using MediatR;
using Urban.Finance.Application.Invoices;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Infrastructure.Invoices;

/// <summary>
/// Tarefa em segundo plano que monta e envia a fatura de uma cobrança. A fila guarda só os ids, nunca o PDF nem o e-mail. Sem
/// requisição não há token, então a tarefa escolhe o condomínio antes de qualquer consulta; é por isso que o id dele vai na fila.
/// A retentativa é a dos outros e-mails (5 depois da primeira: 60 s, 5 min, 15 min, 1 h, 3 h). Só a falha do provedor é lançada
/// e tentada de novo; as que não adiantam repetir (sem e-mail, PDF que não monta) voltam como resultado e a tarefa termina.
/// </summary>
public sealed class SendInvoiceJob(ITenantScope tenant, ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(Guid condominiumId, Guid chargeId, CancellationToken cancellationToken)
    {
        tenant.Enter(condominiumId);
        await sender.Send(new SendInvoiceCommand(chargeId), cancellationToken);
    }
}
