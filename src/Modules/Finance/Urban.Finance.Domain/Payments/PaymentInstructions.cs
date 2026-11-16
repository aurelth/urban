using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

/// <summary>
/// Os dados para o morador pagar a fatura: a chave Pix e a conta bancária do condomínio. Há uma linha por condomínio, e cada uma das
/// duas partes é opcional (o síndico pode cadastrar só o Pix, só a conta ou nenhuma).
/// </summary>
public sealed class PaymentInstructions
{
    private PaymentInstructions(Guid condominiumId, PixKey? pix, BankAccount? account, DateTimeOffset updatedAt)
    {
        CondominiumId = condominiumId;
        Pix = pix;
        Account = account;
        UpdatedAt = updatedAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private PaymentInstructions()
    {
    }

    public Guid CondominiumId { get; }

    public PixKey? Pix { get; private set; }

    public BankAccount? Account { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<PaymentInstructions> Create(Guid condominiumId, PixKey? pix, BankAccount? account, DateTimeOffset now) =>
        condominiumId == Guid.Empty
            ? Result.Failure<PaymentInstructions>(FinanceErrors.CondominiumNotFound)
            : Result.Success(new PaymentInstructions(condominiumId, pix, account, now));

    public void Replace(PixKey? pix, BankAccount? account, DateTimeOffset now)
    {
        Pix = pix;
        Account = account;
        UpdatedAt = now;
    }
}
