using Urban.Identity.Domain.Consent;

namespace Urban.Identity.Infrastructure.Consent;

/// <summary>Versão vigente dos termos de uso e da política de privacidade (seção "Consent"). Muda quando os textos mudam, então vem da configuração.</summary>
public sealed class ConsentOptions
{
    public const string SectionName = "Consent";

    /// <summary>Identificador da versão, por exemplo a data de publicação dos textos (<c>2026-10-26</c>).</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    public string GetCurrentVersion() =>
        !string.IsNullOrWhiteSpace(CurrentVersion) && CurrentVersion.Length <= UserConsent.MaxVersionLength
            ? CurrentVersion
            : throw new InvalidOperationException(
                $"Falta a versão dos termos de uso ({SectionName}:{nameof(CurrentVersion)}) ou ela passa de {UserConsent.MaxVersionLength} caracteres.");
}