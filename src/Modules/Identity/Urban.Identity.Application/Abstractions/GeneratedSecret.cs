namespace Urban.Identity.Application.Abstractions;

/// <summary>Segredo recém-gerado: o <see cref="Value"/> vai para o cliente (cookie ou link); só o <see cref="Hash"/> é guardado.</summary>
public sealed record GeneratedSecret(string Value, string Hash);
