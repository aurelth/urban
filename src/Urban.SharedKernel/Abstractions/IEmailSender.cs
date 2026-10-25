namespace Urban.SharedKernel.Abstractions;

/// <summary>Porta de saída de e-mail. Quem envia (Brevo, registro em log) é detalhe de infraestrutura e pode ser trocado sem tocar nas regras.</summary>
public interface IEmailSender
{
    /// <exception cref="EmailDeliveryException">O provedor recusou o envio ou não respondeu.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}