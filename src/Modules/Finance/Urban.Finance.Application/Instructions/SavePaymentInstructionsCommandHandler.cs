using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Instructions;

public sealed class SavePaymentInstructionsCommandHandler(IPaymentInstructionsStore store, ITenantContext tenant, IClock clock)
    : IRequestHandler<SavePaymentInstructionsCommand, Result<PaymentInstructionsView>>
{
    public async Task<Result<PaymentInstructionsView>> Handle(SavePaymentInstructionsCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<PaymentInstructionsView>(FinanceErrors.CondominiumNotFound);
        }

        Result<PixKey?> pix = ParsePix(request.Pix);
        if (pix.IsFailure)
        {
            return Result.Failure<PaymentInstructionsView>(pix.Error);
        }

        Result<BankAccount?> account = ParseAccount(request.Account);
        if (account.IsFailure)
        {
            return Result.Failure<PaymentInstructionsView>(account.Error);
        }

        Result<PaymentInstructions> prepared = await PrepareAsync(condominiumId, pix.Value, account.Value, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<PaymentInstructionsView>(prepared.Error);
        }

        Result saved = await store.SaveAsync(prepared.Value, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(PaymentInstructionsView.From(prepared.Value))
            : Result.Failure<PaymentInstructionsView>(saved.Error);
    }

    private static Result<PixKey?> ParsePix(PixKeyInput? input)
    {
        if (input is null)
        {
            return Result.Success<PixKey?>(null);
        }

        Result<PixKey> pix = PixKey.Create(input.Kind, input.Key);

        return pix.IsSuccess ? Result.Success<PixKey?>(pix.Value) : Result.Failure<PixKey?>(pix.Error);
    }

    private static Result<BankAccount?> ParseAccount(BankAccountInput? input)
    {
        if (input is null)
        {
            return Result.Success<BankAccount?>(null);
        }

        Result<BankAccount> account = BankAccount.Create(input.Bank, input.Agency, input.Number, input.Kind);

        return account.IsSuccess ? Result.Success<BankAccount?>(account.Value) : Result.Failure<BankAccount?>(account.Error);
    }

    private async Task<Result<PaymentInstructions>> PrepareAsync(Guid condominiumId, PixKey? pix, BankAccount? account, CancellationToken cancellationToken)
    {
        PaymentInstructions? existing = await store.FindAsync(cancellationToken);

        if (existing is null)
        {
            return PaymentInstructions.Create(condominiumId, pix, account, clock.UtcNow);
        }

        existing.Replace(pix, account, clock.UtcNow);

        return Result.Success(existing);
    }
}
