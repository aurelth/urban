using Urban.Api.Endpoints;
using Urban.Finance.Application.Instructions;
using Urban.Finance.Domain.Payments;

namespace Urban.Api.Mapping;

/// <summary>Do corpo para o comando e do resultado para a resposta. Os tipos vão em texto minúsculo.</summary>
public static class PaymentInstructionsMapper
{
    public static SavePaymentInstructionsCommand ToCommand(PaymentInstructionsRequest request) => new(
        request.Pix is { } pix ? new PixKeyInput(ParsePixKind(pix.Kind), pix.Key) : null,
        request.Account is { } account ? new BankAccountInput(account.Bank, account.Agency, account.Number, ParseAccountKind(account.Kind)) : null);

    /// <summary>Texto desconhecido vira um valor fora do enum, que o Domain recusa com o código do tipo da chave.</summary>
    public static PixKeyKind ParsePixKind(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "cnpj" => PixKeyKind.Cnpj,
        "cpf" => PixKeyKind.Cpf,
        "email" => PixKeyKind.Email,
        "phone" => PixKeyKind.Phone,
        "random" => PixKeyKind.Random,
        _ => (PixKeyKind)(-1),
    };

    public static BankAccountKind ParseAccountKind(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "checking" => BankAccountKind.Checking,
        "savings" => BankAccountKind.Savings,
        _ => (BankAccountKind)(-1),
    };

    public static PaymentInstructionsResponse ToResponse(PaymentInstructionsView view) => new(
        view.Pix is { } pix ? new PixKeyResponse(PixKindText(pix.Kind), pix.Key) : null,
        view.Account is { } account ? new BankAccountResponse(account.Bank, account.Agency, account.Number, AccountKindText(account.Kind)) : null,
        view.UpdatedAt);

    private static string PixKindText(PixKeyKind kind) => kind switch
    {
        PixKeyKind.Cnpj => "cnpj",
        PixKeyKind.Cpf => "cpf",
        PixKeyKind.Email => "email",
        PixKeyKind.Phone => "phone",
        _ => "random",
    };

    private static string AccountKindText(BankAccountKind kind) => kind == BankAccountKind.Savings ? "savings" : "checking";
}
