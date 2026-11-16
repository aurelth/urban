using FluentAssertions;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

public class PaymentTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid ChargeId = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly YearMonth Competence = YearMonth.Create(2026, 10).Value;

    private static Result<Payment> Register(
        decimal amount = 300m,
        DateOnly? receivedOn = null,
        PaymentMethod method = PaymentMethod.Pix,
        string? note = null) =>
        Payment.Register(CondominiumId, ChargeId, Competence, amount, receivedOn ?? Today, method, note, Manager, Now, Today);

    private static Payment Registered() => Register().Value;

    [Fact]
    public void Register_KeepsEverythingTheManagerInformedAndWhoRegisteredIt()
    {
        Result<Payment> result = Register(300.50m, new DateOnly(2026, 11, 5), PaymentMethod.Transfer, "Pagou na portaria");

        result.IsSuccess.Should().BeTrue();
        Payment payment = result.Value;
        payment.Id.Should().NotBe(Guid.Empty);
        payment.CondominiumId.Should().Be(CondominiumId);
        payment.ChargeId.Should().Be(ChargeId);
        payment.Amount.Should().Be(300.50m);
        payment.ReceivedOn.Should().Be(new DateOnly(2026, 11, 5));
        payment.Method.Should().Be(PaymentMethod.Transfer);
        payment.Note.Should().Be("Pagou na portaria");
        payment.RegisteredBy.Should().Be(Manager);
        payment.RegisteredAt.Should().Be(Now);
        payment.IsReversed.Should().BeFalse();
        payment.ReversedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.005)]
    [InlineData(10000000)]
    public void Register_RefusesAnAmountThatIsNotPositiveWithAtMostTwoDecimalsUpToTheLimit(double amount)
    {
        Register((decimal)amount).Error.Should().Be(PaymentErrors.AmountInvalid);
    }

    [Fact]
    public void Register_AcceptsTheLargestAmountAndOneCent()
    {
        Register(9_999_999.99m).IsSuccess.Should().BeTrue();
        Register(0.01m).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Register_RefusesAMethodThatIsNotOneOfTheFive(int method)
    {
        Register(method: (PaymentMethod)method).Error.Should().Be(PaymentErrors.MethodInvalid);
    }

    [Theory]
    [InlineData(PaymentMethod.Pix)]
    [InlineData(PaymentMethod.Boleto)]
    [InlineData(PaymentMethod.Transfer)]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Other)]
    public void Register_AcceptsEachMethod(PaymentMethod method)
    {
        Register(method: method).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Register_AcceptsToday_ButNotTomorrow()
    {
        Register(receivedOn: Today).IsSuccess.Should().BeTrue();
        Register(receivedOn: Today.AddDays(1)).Error.Should().Be(PaymentErrors.DateInvalid);
    }

    [Fact]
    public void Register_AcceptsTheFirstDayTwelveMonthsBeforeTheCompetence_ButNotTheDayBefore()
    {
        DateOnly floor = new(2025, 10, 1);

        Register(receivedOn: floor).IsSuccess.Should().BeTrue();
        Register(receivedOn: floor.AddDays(-1)).Error.Should().Be(PaymentErrors.DateInvalid);
    }

    [Fact]
    public void Register_DoesNotDependOnWhenTheMonthWasClosed()
    {
        // Pagamento anterior ao fechamento e à competência é normal na migração das planilhas.
        Register(receivedOn: new DateOnly(2026, 9, 20)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Register_TrimsTheNote_AndTreatsABlankOneAsNone()
    {
        Register(note: "  Pagou em dinheiro  ").Value.Note.Should().Be("Pagou em dinheiro");
        Register(note: "   ").Value.Note.Should().BeNull();
        Register(note: null).Value.Note.Should().BeNull();
    }

    [Fact]
    public void Register_AcceptsANoteOf200Characters_ButNotOf201()
    {
        Register(note: new string('a', 200)).IsSuccess.Should().BeTrue();
        Register(note: new string('a', 201)).Error.Should().Be(PaymentErrors.NoteInvalid);
    }

    [Fact]
    public void Register_ChecksTheAmountFirst_ThenTheMethod_ThenTheDate_ThenTheNote()
    {
        Register(0m, Today.AddDays(1), (PaymentMethod)0, new string('a', 201)).Error.Should().Be(PaymentErrors.AmountInvalid);
        Register(10m, Today.AddDays(1), (PaymentMethod)0, new string('a', 201)).Error.Should().Be(PaymentErrors.MethodInvalid);
        Register(10m, Today.AddDays(1), PaymentMethod.Pix, new string('a', 201)).Error.Should().Be(PaymentErrors.DateInvalid);
    }

    // ---- Reverse ----

    [Fact]
    public void Reverse_MarksThePaymentAsReversedWithWhoWhenAndWhy()
    {
        Payment payment = Registered();
        Guid other = Guid.CreateVersion7();
        DateTimeOffset later = Now.AddHours(3);

        Result result = payment.Reverse("  Lançado na unidade errada  ", other, later);

        result.IsSuccess.Should().BeTrue();
        payment.IsReversed.Should().BeTrue();
        payment.ReversedAt.Should().Be(later);
        payment.ReversedBy.Should().Be(other);
        payment.ReversalReason.Should().Be("Lançado na unidade errada");
        payment.Amount.Should().Be(300m);
    }

    [Fact]
    public void Reverse_IsFinal_AReversedPaymentCannotBeReversedAgain()
    {
        Payment payment = Registered();
        payment.Reverse("Erro", Manager, Now);

        Result again = payment.Reverse("Outro motivo", Manager, Now.AddHours(1));

        again.Error.Should().Be(PaymentErrors.AlreadyReversed);
        payment.ReversalReason.Should().Be("Erro");
        payment.ReversedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reverse_NeedsAReason(string? reason)
    {
        Payment payment = Registered();

        payment.Reverse(reason, Manager, Now).Error.Should().Be(PaymentErrors.ReasonInvalid);
        payment.IsReversed.Should().BeFalse();
    }

    [Fact]
    public void Reverse_AcceptsAReasonOf200Characters_ButNotOf201()
    {
        Registered().Reverse(new string('a', 200), Manager, Now).IsSuccess.Should().BeTrue();
        Registered().Reverse(new string('a', 201), Manager, Now).Error.Should().Be(PaymentErrors.ReasonInvalid);
    }
}
