namespace Urban.Identity.Domain.Contact;

/// <summary>Um arquivo como chegou da requisição, ainda sem conferir. Só é usado para validar e enviar: o Urban não guarda o conteúdo.</summary>
public sealed record ContactReplyFile(string FileName, byte[] Content)
{
    public override string ToString() => nameof(ContactReplyFile);
}
