using Urban.SharedKernel;

namespace Urban.Identity.Domain.Consent;

/// <summary>Regra do aceite: só vale a versão vigente dos termos, comparada letra a letra. Aceitar uma versão antiga não conta.</summary>
public static class ConsentPolicy
{
    public static Result Validate(string? acceptedVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(acceptedVersion))
        {
            return Result.Failure(ConsentErrors.Required);
        }

        return string.Equals(acceptedVersion, currentVersion, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(ConsentErrors.Outdated);
    }
}