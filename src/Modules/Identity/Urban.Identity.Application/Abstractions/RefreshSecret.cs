namespace Urban.Identity.Application.Abstractions;

/// <summary>Segredo de renovação recém-gerado: o <see cref="Value"/> vai para o cliente; só o <see cref="Hash"/> é guardado.</summary>
public sealed record RefreshSecret(string Value, string Hash);
