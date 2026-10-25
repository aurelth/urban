namespace Urban.EmailDelivery;

public enum EmailProvider
{
    /// <summary>Não envia: só registra em log que o envio aconteceria. Para desenvolvimento e testes.</summary>
    Log,
    Brevo,
}