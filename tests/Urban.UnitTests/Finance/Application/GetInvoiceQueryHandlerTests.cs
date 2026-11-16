using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O PDF da cobrança de uma unidade. Hoje é 7/11/2026; outubro está fechado com duas cobranças, 600 (unidade 101) e 400 (bloco A, 102),
/// vencendo em 10/11. A fatura leva também a situação do pagamento, a base do rateio, o demonstrativo do mês, as pendências
/// anteriores, os dados para pagar e o contato do síndico.
/// </summary>
public class GetInvoiceQueryHandlerTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly MonthClosing _closing;
    private readonly UnitCharge _first;
    private readonly UnitCharge _second;

    public GetInvoiceQueryHandlerTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, FinanceTestKit.M(2026, 10), 1000m);
        _kit.NewEntry(expense, FinanceTestKit.M(2026, 10));
        _closing = _kit.CloseMonth(FinanceTestKit.M(2026, 10), new DateOnly(2026, 11, 10));
        _first = _closing.Charges.Single(charge => charge.Number == "101");
        _second = _closing.Charges.Single(charge => charge.Number == "102");
    }

    private GetInvoiceQueryHandler Handler(FinanceTestKit? kit = null, IClock? clock = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new GetInvoiceQueryHandler(used.Charges, used.Payments, used.Instructions, used.Parties, used.Renderer, used.Tenant, clock ?? used.Clock);
    }

    private Payment Pay(UnitCharge charge, decimal amount, YearMonth? month = null, bool reversed = false)
    {
        Payment payment = Payment.Register(
            _kit.CondominiumId, charge.Id, month ?? FinanceTestKit.M(2026, 10), amount, new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Guid.CreateVersion7(),
            FinanceTestKit.Now, new DateOnly(2026, 11, 7)).Value;

        if (reversed)
        {
            payment.Reverse("Lançado por engano", Guid.CreateVersion7(), FinanceTestKit.Now).IsSuccess.Should().BeTrue();
        }

        return _kit.Payments.Existing(payment);
    }

    private MonthClosing CloseSeptember(decimal amount = 500m)
    {
        Expense expense = _kit.NewSingle(_kit.NewGroup("Despesas de setembro"), FinanceTestKit.M(2026, 9), amount);
        _kit.NewEntry(expense, FinanceTestKit.M(2026, 9));

        return _kit.CloseMonth(FinanceTestKit.M(2026, 9), new DateOnly(2026, 10, 10));
    }

    private async Task<InvoiceDocument> RenderedAsync(UnitCharge charge, FinanceTestKit? kit = null, IClock? clock = null)
    {
        FinanceTestKit used = kit ?? _kit;
        (await Handler(used, clock).Handle(new GetInvoiceQuery(charge.Id), default)).IsSuccess.Should().BeTrue();

        return used.Renderer.Rendered.Single();
    }

    [Fact]
    public async Task Handle_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<InvoiceFile> result = await Handler(kit).Handle(new GetInvoiceQuery(_first.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Renderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AChargeThatDoesNotExist_Fails()
    {
        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Renderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheCondominiumDataIsMissing_Fails()
    {
        _kit.Parties.Condominium = null;

        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(_first.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        _kit.Renderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsTheRenderedFileWithASafeName()
    {
        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(_second.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("fatura-2026-10-bloco-a-102.pdf");
        result.Value.Content.Should().Equal(FakeInvoiceRenderer.Output);
        _kit.Renderer.Rendered.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_FillsTheDocumentFromTheClosedChargeAndTheCondominium()
    {
        await Handler().Handle(new GetInvoiceQuery(_first.Id), default);

        InvoiceDocument document = _kit.Renderer.Rendered.Single();
        document.CondominiumName.Should().Be("Residencial Aurora");
        document.CondominiumCnpj.Should().Be("12.345.678/0001-95");
        document.CondominiumAddress.Should().Be("Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000");
        document.UnitLabel.Should().Be("101");
        document.Month.Should().Be(FinanceTestKit.M(2026, 10));
        document.DueDate.Should().Be(new DateOnly(2026, 11, 10));
        document.IssuedOn.Should().Be(new DateOnly(2026, 11, 7));
        document.Total.Should().Be(600m);
        document.Reference.Should().Be($"2026-10-{_first.Id.ToString("N")[^8..].ToUpperInvariant()}");
        document.Lines.Select(line => (line.Label, line.Amount)).Should().Equal(("Despesas", 600m));
    }

    [Fact]
    public async Task Handle_TheLinesFollowTheOrderOfTheCharge_AndSumToTheTotal()
    {
        await Handler().Handle(new GetInvoiceQuery(_second.Id), default);

        InvoiceDocument document = _kit.Renderer.Rendered.Single();
        document.Lines.Select(line => line.Label).Should().Equal(_second.Lines.OrderBy(line => line.Position).Select(line => line.Label));
        document.Lines.Sum(line => line.Amount).Should().Be(document.Total);
    }

    [Fact]
    public async Task Handle_NamesTheResponsibleOfTheUnit_AsOfTodayInBrasilia()
    {
        _kit.Parties.Responsibles[_first.UnitId] = new InvoiceResponsible("Marina Souza", "marina@example.com");

        await Handler().Handle(new GetInvoiceQuery(_first.Id), default);

        _kit.Renderer.Rendered.Single().ResponsibleName.Should().Be("Marina Souza");
        _kit.Parties.AskedOn.Should().Be(new DateOnly(2026, 11, 7));
        _kit.Parties.AskedAbout.Should().Equal(_first.UnitId);
    }

    [Fact]
    public async Task Handle_AUnitWithoutResponsible_StillGetsTheInvoice()
    {
        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(_first.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Renderer.Rendered.Single().ResponsibleName.Should().BeNull();
    }

    // ---- Situação do pagamento ----

    [Fact]
    public async Task Handle_AChargeNobodyPaid_IsOpenWithTheWholeBalance()
    {
        InvoiceDocument document = await RenderedAsync(_first);

        document.Situation.Should().Be(new InvoiceSituation(ChargeStatus.Open, 0m, 600m));
    }

    [Fact]
    public async Task Handle_ACharge_PaidInPart_IsPartialWithWhatIsLeft()
    {
        Pay(_first, 200m);

        InvoiceDocument document = await RenderedAsync(_first);

        document.Situation.Should().Be(new InvoiceSituation(ChargeStatus.Partial, 200m, 400m));
    }

    [Fact]
    public async Task Handle_AChargePaidInFull_IsPaidWithNothingLeft()
    {
        Pay(_first, 250m);
        Pay(_first, 350m);

        InvoiceDocument document = await RenderedAsync(_first);

        document.Situation.Should().Be(new InvoiceSituation(ChargeStatus.Paid, 600m, 0m));
    }

    [Fact]
    public async Task Handle_AReversedPayment_DoesNotCount()
    {
        Pay(_first, 600m, reversed: true);

        InvoiceDocument document = await RenderedAsync(_first);

        document.Situation.Should().Be(new InvoiceSituation(ChargeStatus.Open, 0m, 600m));
    }

    [Fact]
    public async Task Handle_ThePaymentsOfAnotherChargeDoNotCount()
    {
        Pay(_second, 400m);

        InvoiceDocument document = await RenderedAsync(_first);

        document.Situation.Status.Should().Be(ChargeStatus.Open);
    }

    [Fact]
    public async Task Handle_AfterTheDueDate_AnUnpaidChargeIsOverdue_AsOfTheDayOfTheRequest()
    {
        IClock afterDueDate = new FixedClock(new DateTimeOffset(2026, 11, 12, 15, 0, 0, TimeSpan.Zero));

        InvoiceDocument document = await RenderedAsync(_first, clock: afterDueDate);

        document.Situation.Should().Be(new InvoiceSituation(ChargeStatus.Overdue, 0m, 600m));
        document.IssuedOn.Should().Be(new DateOnly(2026, 11, 7), "a emissão é o dia em que o mês foi fechado, não o da consulta");
    }

    // ---- Base do rateio ----

    [Fact]
    public async Task Handle_ShowsTheShareOfEachUnitInTheMethodTheCondominiumChose()
    {
        (await RenderedAsync(_first)).Share.Should().Be(new InvoiceShare(ApportionmentBasisKind.IdealFraction, 60m));
        _kit.Renderer.Rendered.Clear();
        (await RenderedAsync(_second)).Share.Should().Be(new InvoiceShare(ApportionmentBasisKind.IdealFraction, 40m));
    }

    [Fact]
    public async Task Handle_TheShareFollowsTheWeightsOfTheClosing_WhateverTheMethod()
    {
        FinanceTestKit kit = new();
        kit.Basis.Outcome = Result.Success(new ApportionmentBasis(
            ApportionmentBasisKind.Area,
            [new ApportionmentUnit(Guid.CreateVersion7(), null, "101", 50m), new ApportionmentUnit(Guid.CreateVersion7(), "A", "102", 150m)]));
        kit.NewEntry(kit.NewFixed(kit.NewGroup(), FinanceTestKit.M(2026, 10), 1000m), FinanceTestKit.M(2026, 10));
        UnitCharge small = kit.CloseMonth(FinanceTestKit.M(2026, 10)).Charges.Single(charge => charge.Number == "101");

        InvoiceDocument document = await RenderedAsync(small, kit);

        document.Share.Should().Be(new InvoiceShare(ApportionmentBasisKind.Area, 25m));
    }

    // ---- Demonstrativo dos gastos ----

    [Fact]
    public async Task Handle_TheStatementIsTheCondominiumTotalOfTheMonth_NotTheShareOfTheUnit()
    {
        InvoiceDocument document = await RenderedAsync(_second);

        document.Statement.Should().Equal(new InvoiceStatementLine("Despesas", 1000m));
        document.StatementTotal.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_TheStatementListsTheGroupsAndTheReserveFund_ButLeavesTheIndividualConsumptionOut()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup doorman = kit.NewGroup("Portaria");
        ExpenseGroup cleaning = kit.NewGroup("Limpeza");
        kit.NewEntry(kit.NewFixed(doorman, month, 700m, "Porteiro"), month);
        kit.NewEntry(kit.NewFixed(cleaning, month, 300m, "Faxineira"), month);
        ConsumptionTariff tariff = ConsumptionTariff.Create(
            kit.CondominiumId, month, TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(null, 0m, 8.63m)]).Value, null, 100m, null, FinanceTestKit.Now).Value;
        MeterReading[] readings =
        [
            MeterReading.Create(kit.CondominiumId, FakeApportionmentBasis.First.UnitId, month, ConsumptionService.Water, 100m, 112m, FinanceTestKit.Now).Value,
            MeterReading.Create(kit.CondominiumId, FakeApportionmentBasis.Second.UnitId, month, ConsumptionService.Water, 50m, 58m, FinanceTestKit.Now).Value,
        ];
        ClosingOptions options = new(
            new Dictionary<Guid, string> { [doorman.Id] = "Portaria", [cleaning.Id] = "Limpeza" }, new HashSet<Guid> { doorman.Id }, 10m, tariff, readings);
        UnitCharge charge = kit.CloseMonth(month, options: options).Charges.Single(candidate => candidate.Number == "101");
        charge.Lines.Select(line => line.Kind).Should().Contain(ChargeLineKind.Water, "o recibo da unidade tem consumo, o demonstrativo não");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Select(line => (line.Label, line.Amount)).Should().Equal(("Limpeza", 300m), ("Portaria", 700m), ("Fundo de reserva", 70m));
        document.StatementTotal.Should().Be(1070m);
    }

    // ---- Pendências anteriores ----

    [Fact]
    public async Task Handle_WithoutEarlierCharges_TheListIsEmpty()
    {
        InvoiceDocument document = await RenderedAsync(_first);

        document.Earlier.Should().BeEmpty();
        document.EarlierBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ListsTheEarlierChargesOfTheSameUnitThatStillOweSomething()
    {
        MonthClosing september = CloseSeptember();
        UnitCharge septemberFirst = september.Charges.Single(charge => charge.Number == "101");
        Pay(septemberFirst, 100m, FinanceTestKit.M(2026, 9));

        InvoiceDocument document = await RenderedAsync(_first);

        document.Earlier.Should().Equal(new InvoiceEarlierCharge(FinanceTestKit.M(2026, 9), new DateOnly(2026, 10, 10), 300m, 100m));
        document.Earlier.Single().Balance.Should().Be(200m);
        document.EarlierBalance.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_AnEarlierChargeThatWasPaidInFullIsNotListed()
    {
        MonthClosing september = CloseSeptember();
        Pay(september.Charges.Single(charge => charge.Number == "101"), 300m, FinanceTestKit.M(2026, 9));

        InvoiceDocument document = await RenderedAsync(_first);

        document.Earlier.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AnEarlierPaymentThatWasReversedMakesTheChargeOweAgain()
    {
        MonthClosing september = CloseSeptember();
        Pay(september.Charges.Single(charge => charge.Number == "101"), 300m, FinanceTestKit.M(2026, 9), reversed: true);

        InvoiceDocument document = await RenderedAsync(_first);

        document.Earlier.Single().Paid.Should().Be(0m);
        document.EarlierBalance.Should().Be(300m);
    }

    [Fact]
    public async Task Handle_TheEarlierListIgnoresOtherUnitsAndLaterMonths()
    {
        CloseSeptember();
        Expense november = _kit.NewSingle(_kit.NewGroup("Despesas de novembro"), FinanceTestKit.M(2026, 11), 200m);
        _kit.NewEntry(november, FinanceTestKit.M(2026, 11));
        _kit.CloseMonth(FinanceTestKit.M(2026, 11), new DateOnly(2026, 12, 10));

        InvoiceDocument document = await RenderedAsync(_second);

        document.Earlier.Select(charge => charge.Month).Should().Equal(FinanceTestKit.M(2026, 9));
        document.Earlier.Single().Amount.Should().Be(200m, "a unidade 102 pagava 40% de setembro, não o total nem a parte da 101");
    }

    [Fact]
    public async Task Handle_TheEarlierListIsCapped()
    {
        await RenderedAsync(_first);

        _kit.Charges.AskedLimit.Should().Be(GetInvoiceQueryHandler.EarlierChargesLimit);
        GetInvoiceQueryHandler.EarlierChargesLimit.Should().Be(36);
    }

    // ---- Como pagar e contato ----

    [Fact]
    public async Task Handle_WithoutPaymentData_ThereIsNoHowToPay()
    {
        (await RenderedAsync(_first)).HowToPay.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PaymentDataWithNeitherPixNorAccount_IsTreatedAsNothing()
    {
        _kit.Instructions.Existing(PaymentInstructions.Create(_kit.CondominiumId, null, null, FinanceTestKit.Now).Value);

        (await RenderedAsync(_first)).HowToPay.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShowsThePixKeyFormattedAndLabeled()
    {
        _kit.Instructions.Existing(PaymentInstructions.Create(
            _kit.CondominiumId, PixKey.Create(PixKeyKind.Cnpj, "12345678000195").Value, null, FinanceTestKit.Now).Value);

        InvoiceDocument document = await RenderedAsync(_first);

        document.HowToPay.Should().Be(new InvoiceHowToPay(new InvoicePix("CNPJ", "12.345.678/0001-95"), null));
    }

    [Fact]
    public async Task Handle_ShowsTheBankAccount()
    {
        _kit.Instructions.Existing(PaymentInstructions.Create(
            _kit.CondominiumId, null, BankAccount.Create("Banco Exemplo", "1234-5", "12345-6", BankAccountKind.Savings).Value, FinanceTestKit.Now).Value);

        InvoiceDocument document = await RenderedAsync(_first);

        document.HowToPay.Should().Be(new InvoiceHowToPay(null, new InvoiceAccount("Banco Exemplo", "1234-5", "12345-6", "Conta poupança")));
    }

    [Fact]
    public async Task Handle_ShowsBothWhenTheCondominiumRegisteredBoth()
    {
        _kit.Instructions.Existing(PaymentInstructions.Create(
            _kit.CondominiumId,
            PixKey.Create(PixKeyKind.Email, "financeiro@aurora.com.br").Value,
            BankAccount.Create("Banco Exemplo", "1234", "12345", BankAccountKind.Checking).Value,
            FinanceTestKit.Now).Value);

        InvoiceDocument document = await RenderedAsync(_first);

        document.HowToPay!.Pix.Should().Be(new InvoicePix("E-mail", "financeiro@aurora.com.br"));
        document.HowToPay.Account.Should().Be(new InvoiceAccount("Banco Exemplo", "1234", "12345", "Conta corrente"));
    }

    [Fact]
    public async Task Handle_ShowsTheManagerContact()
    {
        InvoiceDocument document = await RenderedAsync(_first);

        document.Manager.Should().Be(new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"));
    }

    [Fact]
    public async Task Handle_WithoutAManager_TheInvoiceStillComes()
    {
        _kit.Parties.Condominium = _kit.Parties.Condominium! with { Manager = null };

        InvoiceDocument document = await RenderedAsync(_first);

        document.Manager.Should().BeNull();
    }
}
