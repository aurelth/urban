namespace Urban.SharedKernel.Abstractions;

/// <summary>O provedor de e-mail recusou o envio ou não respondeu. A mensagem nunca carrega o destinatário, o corpo nem credenciais.</summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}