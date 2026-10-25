namespace Urban.SharedKernel.Abstractions;

public sealed record EmailMessage(string To, string Subject, string TextBody)
{
    /// <summary>Parte do endereço depois do @: o que se pode registrar em log sem expor quem recebeu.</summary>
    public string RecipientDomain => To[(To.IndexOf('@') + 1)..];
}