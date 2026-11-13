namespace Urban.Identity.Application.Contact;

/// <summary>O que o síndico recebe ao enviar: o identificador e o momento do registro.</summary>
public sealed record ContactMessageReceipt(Guid Id, DateTimeOffset SentAt);
