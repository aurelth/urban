using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

/// <summary>
/// Um recebimento registrado à mão pelo síndico contra a cobrança de uma unidade. Nunca é apagado: o erro se corrige com o
/// estorno, que marca o pagamento e o tira da soma, deixando quem, quando e por quê.
/// </summary>
public sealed class Payment
{
    public const int NoteMaxLength = 200;
    public const int ReasonMaxLength = 200;

    /// <summary>Quantos meses antes da competência a data de recebimento ainda é aceita (pagamento adiantado de migração das planilhas).</summary>
    public const int MonthsBeforeCompetence = 12;

    private Payment(
        Guid condominiumId, Guid chargeId, decimal amount, DateOnly receivedOn, PaymentMethod method, string? note, Guid registeredBy, DateTimeOffset registeredAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        ChargeId = chargeId;
        Amount = amount;
        ReceivedOn = receivedOn;
        Method = method;
        Note = note;
        RegisteredBy = registeredBy;
        RegisteredAt = registeredAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private Payment()
    {
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid ChargeId { get; }

    public decimal Amount { get; }

    public DateOnly ReceivedOn { get; }

    public PaymentMethod Method { get; }

    public string? Note { get; }

    public Guid RegisteredBy { get; }

    public DateTimeOffset RegisteredAt { get; }

    public DateTimeOffset? ReversedAt { get; private set; }

    public Guid? ReversedBy { get; private set; }

    public string? ReversalReason { get; private set; }

    public bool IsReversed => ReversedAt is not null;

    /// <summary>
    /// Confere e cria o pagamento. Vale o primeiro erro, na ordem valor, forma, data e observação. A data não pode ser futura (pelo
    /// dia de Brasília, <paramref name="today"/>) nem anterior ao primeiro dia de 12 meses antes da competência; ela não depende de
    /// quando o mês foi fechado.
    /// </summary>
    public static Result<Payment> Register(
        Guid condominiumId,
        Guid chargeId,
        YearMonth competence,
        decimal amount,
        DateOnly receivedOn,
        PaymentMethod method,
        string? note,
        Guid registeredBy,
        DateTimeOffset now,
        DateOnly today)
    {
        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure<Payment>(PaymentErrors.AmountInvalid);
        }

        if (!Enum.IsDefined(method))
        {
            return Result.Failure<Payment>(PaymentErrors.MethodInvalid);
        }

        if (receivedOn > today || receivedOn < competence.AddMonths(-MonthsBeforeCompetence).FirstDay)
        {
            return Result.Failure<Payment>(PaymentErrors.DateInvalid);
        }

        string normalizedNote = TextRules.Normalize(note);

        if (normalizedNote.Length > NoteMaxLength || TextRules.HasControlCharacters(note))
        {
            return Result.Failure<Payment>(PaymentErrors.NoteInvalid);
        }

        return Result.Success(new Payment(
            condominiumId, chargeId, amount, receivedOn, method, normalizedNote.Length == 0 ? null : normalizedNote, registeredBy, now));
    }

    /// <summary>Estorna o pagamento. O motivo é obrigatório e o estorno é definitivo: estornado não volta e não se estorna duas vezes.</summary>
    public Result Reverse(string? reason, Guid reversedBy, DateTimeOffset now)
    {
        if (IsReversed)
        {
            return Result.Failure(PaymentErrors.AlreadyReversed);
        }

        if (!TextRules.IsValidText(reason, ReasonMaxLength, out string normalized))
        {
            return Result.Failure(PaymentErrors.ReasonInvalid);
        }

        ReversedAt = now;
        ReversedBy = reversedBy;
        ReversalReason = normalized;

        return Result.Success();
    }
}
