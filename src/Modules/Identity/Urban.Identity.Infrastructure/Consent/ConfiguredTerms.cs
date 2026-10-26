using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Consent;

/// <summary>Os termos vigentes, lidos da configuração na partida.</summary>
public sealed record ConfiguredTerms(string Version) : ICurrentTerms;