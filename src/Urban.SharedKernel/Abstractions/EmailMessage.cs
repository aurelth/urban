namespace Urban.SharedKernel.Abstractions;

/// <param name="ReplyTo">Para onde vai a resposta de quem recebe; sem ele, vale o remetente (um endereço "não responda").</param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string? ReplyTo = null,
    IReadOnlyList<EmailAttachment>? Attachments = null)
{
    /// <summary>Parte do endereço depois do @: o que se pode registrar em log sem expor quem recebeu.</summary>
    public string RecipientDomain => To[(To.IndexOf('@') + 1)..];
}

public sealed record EmailAttachment(string FileName, byte[] Content);
