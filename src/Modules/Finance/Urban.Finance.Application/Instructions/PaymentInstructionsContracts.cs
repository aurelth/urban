using MediatR;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Instructions;

public sealed record PixKeyView(PixKeyKind Kind, string Key);

public sealed record BankAccountView(string Bank, string Agency, string Number, BankAccountKind Kind);

/// <param name="UpdatedAt">Nulo enquanto o condomínio não cadastrou nada.</param>
public sealed record PaymentInstructionsView(PixKeyView? Pix, BankAccountView? Account, DateTimeOffset? UpdatedAt)
{
    public static PaymentInstructionsView Empty { get; } = new(null, null, null);

    public static PaymentInstructionsView From(PaymentInstructions? instructions) => instructions is null
        ? Empty
        : new(
            instructions.Pix is { } pix ? new PixKeyView(pix.Kind, pix.Key) : null,
            instructions.Account is { } account ? new BankAccountView(account.Bank, account.Agency, account.Number, account.Kind) : null,
            instructions.UpdatedAt);
}

public sealed record PixKeyInput(PixKeyKind Kind, string? Key);

public sealed record BankAccountInput(string? Bank, string? Agency, string? Number, BankAccountKind Kind);

public sealed record GetPaymentInstructionsQuery : IRequest<Result<PaymentInstructionsView>>;

/// <summary>Troca tudo de uma vez: o que não vier (nulo) deixa de estar cadastrado.</summary>
public sealed record SavePaymentInstructionsCommand(PixKeyInput? Pix, BankAccountInput? Account) : IRequest<Result<PaymentInstructionsView>>;
