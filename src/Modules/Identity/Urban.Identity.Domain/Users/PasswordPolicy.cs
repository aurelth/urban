using Urban.SharedKernel;

namespace Urban.Identity.Domain.Users;

/// <summary>Regras de senha aceitas pelo Urban: tamanho entre o mínimo e o máximo, com pelo menos uma letra e um dígito.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 128;

    public static readonly Error TooShort =
        new("identity.password.too_short", $"A senha precisa ter pelo menos {MinLength} caracteres");

    public static readonly Error TooLong =
        new("identity.password.too_long", $"A senha pode ter no máximo {MaxLength} caracteres");

    public static readonly Error NeedsLetterAndDigit =
        new("identity.password.needs_letter_and_digit", "A senha precisa ter pelo menos uma letra e um número");

    public static Result Validate(string? password)
    {
        string value = password ?? string.Empty;

        if (value.Length < MinLength)
        {
            return Result.Failure(TooShort);
        }

        if (value.Length > MaxLength)
        {
            return Result.Failure(TooLong);
        }

        return value.Any(char.IsLetter) && value.Any(char.IsDigit)
            ? Result.Success()
            : Result.Failure(NeedsLetterAndDigit);
    }
}
