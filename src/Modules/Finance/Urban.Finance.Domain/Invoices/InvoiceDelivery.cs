using Urban.SharedKernel;

namespace Urban.Finance.Domain.Invoices;

/// <summary>
/// O envio da fatura de uma cobrança por e-mail: uma linha por cobrança, que o envio e cada reenvio reaproveitam. Não guarda o
/// endereço de quem recebeu (o responsável da unidade muda; o e-mail é lido de novo a cada tentativa). A fatura em PDF também
/// não é guardada: sai montada a cada tentativa.
/// </summary>
public sealed class InvoiceDelivery
{
    public const int FailureReasonMaxLength = 30;

    /// <summary>
    /// Depois disto, um envio ainda "na fila" é tratado como perdido (a fila caiu entre gravar e enfileirar): pode ser pedido de novo.
    /// É maior que a soma das esperas das retentativas do Hangfire (cerca de 4h20), então um envio que ainda vai ser tentado não passa por perdido.
    /// </summary>
    public static readonly TimeSpan LostAfter = TimeSpan.FromHours(6);

    private InvoiceDelivery(Guid condominiumId, Guid chargeId, Guid requestedBy, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        ChargeId = chargeId;
        Status = InvoiceDeliveryStatus.Queued;
        RequestedBy = requestedBy;
        RequestedAt = now;
    }

    // Usado só pelo EF Core ao ler do banco.
    private InvoiceDelivery()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid ChargeId { get; }

    public InvoiceDeliveryStatus Status { get; private set; }

    /// <summary>Tentativas do pedido atual (zera a cada reenvio).</summary>
    public int Attempts { get; private set; }

    public Guid RequestedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>Quando a fatura saiu pela última vez. Um reenvio não apaga: continua sendo o último envio que deu certo.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Um dos códigos de <see cref="InvoiceDeliveryFailure"/>; nulo enquanto não falhou.</summary>
    public string? FailureReason { get; private set; }

    public static InvoiceDelivery Queue(Guid condominiumId, Guid chargeId, Guid requestedBy, DateTimeOffset now) =>
        new(condominiumId, chargeId, requestedBy, now);

    /// <summary>Na fila e ainda dentro do prazo de ser tentado; passado o prazo, o envio é tratado como perdido.</summary>
    public bool IsInFlight(DateTimeOffset now) => Status == InvoiceDeliveryStatus.Queued && now - RequestedAt < LostAfter;

    /// <summary>Pede o envio de novo: vale para enviado (reenvio), para falhado e para o perdido. Falha se já está na fila.</summary>
    public Result Requeue(Guid requestedBy, DateTimeOffset now)
    {
        if (IsInFlight(now))
        {
            return Result.Failure(InvoiceDeliveryErrors.AlreadyQueued);
        }

        Status = InvoiceDeliveryStatus.Queued;
        Attempts = 0;
        RequestedBy = requestedBy;
        RequestedAt = now;
        FailureReason = null;

        return Result.Success();
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = InvoiceDeliveryStatus.Sent;
        Attempts++;
        LastAttemptAt = now;
        SentAt = now;
        FailureReason = null;
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = InvoiceDeliveryStatus.Failed;
        Attempts++;
        LastAttemptAt = now;
        FailureReason = reason;
    }
}
