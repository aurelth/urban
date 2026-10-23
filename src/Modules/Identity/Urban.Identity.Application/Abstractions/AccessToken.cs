namespace Urban.Identity.Application.Abstractions;

/// <summary>Token de acesso já assinado (JWT) e o instante em que deixa de valer.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
