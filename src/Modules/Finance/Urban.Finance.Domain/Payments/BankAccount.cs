using System.Text.RegularExpressions;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

/// <summary>A conta bancária do condomínio para transferência ou depósito: banco, agência, conta (cada uma com dígito verificador opcional) e tipo.</summary>
public sealed partial record BankAccount
{
    public const int BankMinLength = 2;
    public const int BankMaxLength = 60;

    private BankAccount(string bank, string agency, string number, BankAccountKind kind)
    {
        Bank = bank;
        Agency = agency;
        Number = number;
        Kind = kind;
    }

    public string Bank { get; }

    public string Agency { get; }

    public string Number { get; }

    public BankAccountKind Kind { get; }

    /// <summary>Vale o primeiro problema, na ordem banco, agência, conta e tipo.</summary>
    public static Result<BankAccount> Create(string? bank, string? agency, string? number, BankAccountKind kind)
    {
        if (!TextRules.IsValidText(bank, BankMaxLength, out string bankName) || bankName.Length < BankMinLength)
        {
            return Result.Failure<BankAccount>(PaymentInstructionsErrors.BankInvalid);
        }

        string normalizedAgency = (agency ?? string.Empty).Trim().ToUpperInvariant();
        if (!AgencyPattern().IsMatch(normalizedAgency))
        {
            return Result.Failure<BankAccount>(PaymentInstructionsErrors.AgencyInvalid);
        }

        string normalizedNumber = (number ?? string.Empty).Trim().ToUpperInvariant();
        if (!NumberPattern().IsMatch(normalizedNumber))
        {
            return Result.Failure<BankAccount>(PaymentInstructionsErrors.NumberInvalid);
        }

        return Enum.IsDefined(kind)
            ? Result.Success(new BankAccount(bankName, normalizedAgency, normalizedNumber, kind))
            : Result.Failure<BankAccount>(PaymentInstructionsErrors.AccountKindInvalid);
    }

    [GeneratedRegex("^[0-9]{1,5}(-[0-9X])?$")]
    private static partial Regex AgencyPattern();

    [GeneratedRegex("^[0-9]{1,12}(-[0-9X])?$")]
    private static partial Regex NumberPattern();
}
