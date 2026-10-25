namespace Urban.SharedKernel.Abstractions;

/// <summary>Porta de saída de e-mail. Quem envia (Brevo, registro em log) é detalhe da Infrastructure e pode ser trocado sem tocar nas regras.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}