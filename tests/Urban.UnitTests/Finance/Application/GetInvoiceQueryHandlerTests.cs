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
/// vencendo em 10/11. A fatura é o pedido de pagamento: leva a participação da unidade, o demonstrativo do mês despesa por despesa,
/// os dados para pagar e o contato do síndico, e nada sobre pagamento já feito (isso é do recibo).
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

        return new GetInvoiceQueryHandler(used.Charges, used.Entries, used.Groups, used.Instructions, used.Parties, used.Housing, used.Renderer, used.Tenant, clock ?? used.Clock);
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

    // ---- O morador pede a própria fatura ----

    private static readonly Guid Resident = Guid.CreateVersion7();

    private static ResidentUnit HomeOf(UnitCharge charge) => new(charge.UnitId, charge.Block, charge.Number);

    private Task<Result<InvoiceFile>> AskAsResident(UnitCharge charge, Guid? userId = null, FinanceTestKit? kit = null) =>
        Handler(kit).Handle(new GetInvoiceQuery(charge.Id, userId ?? Resident), default);

    [Fact]
    public async Task Handle_AResidentGetsTheInvoiceOfTheUnitHeLivesIn_ExactlyTheSameThePdfOfTheManager()
    {
        _kit.Housing.Lives(Resident, HomeOf(_second));
        InvoiceDocument forTheManager = await RenderedAsync(_second);
        _kit.Renderer.Rendered.Clear();

        Result<InvoiceFile> result = await AskAsResident(_second);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("fatura-2026-10-bloco-a-102.pdf");
        _kit.Renderer.Rendered.Single().Should().BeEquivalentTo(forTheManager);
    }

    [Fact]
    public async Task Handle_AResidentCannotGetTheInvoiceOfAnotherUnit_AndItLooksLikeTheChargeDoesNotExist()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first));

        Result<InvoiceFile> result = await AskAsResident(_second);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Renderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AResidentWithTwoUnitsGetsTheInvoiceOfEach()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first), HomeOf(_second));

        (await AskAsResident(_first)).IsSuccess.Should().BeTrue();
        (await AskAsResident(_second)).IsSuccess.Should().BeTrue();
        _kit.Renderer.Rendered.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_AResidentWhoOnlyLivesAsDependentOrWhoLivesNowhereGetsNothing()
    {
        Guid dependent = Guid.CreateVersion7();
        _kit.Housing.LivesAsDependent(dependent);

        (await AskAsResident(_first, dependent)).Error.Should().Be(PaymentErrors.ChargeNotFound);
        (await AskAsResident(_first, Guid.CreateVersion7())).Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Renderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AResidentAskingForAChargeThatDoesNotExist_GetsTheSameAnswerAsForAnotherUnit()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first));

        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(Guid.CreateVersion7(), Resident), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
    }

    [Fact]
    public async Task Handle_TheHousingIsAskedAboutTheAccountOfTheToken_AsOfTodayInBrasilia()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first));

        await AskAsResident(_first);

        _kit.Housing.AskedAbout.Should().Be(Resident);
        _kit.Housing.AskedOn.Should().Be(new DateOnly(2026, 11, 7));
    }

    [Fact]
    public async Task Handle_TheManagerAsksWithoutAnAccountAndTheHousingIsNeverConsulted()
    {
        Result<InvoiceFile> result = await Handler().Handle(new GetInvoiceQuery(_second.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Housing.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithoutACondominium_AResidentGetsNothingAndTheHousingIsNeverConsulted()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<InvoiceFile> result = await AskAsResident(_first, kit: kit);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Housing.Calls.Should().Be(0);
    }

    // ---- Fatura não é recibo ----

    [Fact]
    public void TheDocument_SaysNothingAboutPaymentsAlreadyMade()
    {
        string[] names = [.. typeof(InvoiceDocument).GetProperties().Select(property => property.Name)];

        names.Should().NotContain(["Situation", "Paid", "Balance", "Status", "Earlier", "EarlierBalance"], "a fatura é igual antes e depois de pagar");
    }

    [Fact]
    public async Task Handle_TheInvoiceDoesNotChangeWhenTheDayOfTheRequestChanges()
    {
        InvoiceDocument today = await RenderedAsync(_first);
        _kit.Renderer.Rendered.Clear();
        IClock afterDueDate = new FixedClock(new DateTimeOffset(2026, 12, 20, 15, 0, 0, TimeSpan.Zero));

        InvoiceDocument later = await RenderedAsync(_first, clock: afterDueDate);

        later.Should().BeEquivalentTo(today);
        later.IssuedOn.Should().Be(new DateOnly(2026, 11, 7), "a emissão é o dia em que o mês foi fechado, não o da consulta");
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

    private static ClosingOptions OptionsOf(FinanceTestKit kit, ExpenseGroup[] groups, ExpenseGroup[] reserveBase, decimal reservePercent) =>
        new(
            groups.ToDictionary(group => group.Id, group => group.Name),
            reserveBase.Select(group => group.Id).ToHashSet(),
            reservePercent,
            null,
            []);

    [Fact]
    public async Task Handle_TheStatementIsTheCondominiumTotalOfTheMonth_NotTheShareOfTheUnit()
    {
        InvoiceDocument document = await RenderedAsync(_second);

        InvoiceStatementGroup group = document.Statement.Groups.Single();
        group.Name.Should().Be("Despesas ordinárias");
        group.Items.Select(item => (item.Description, item.Amount)).Should().Equal(("Zeladoria", 1000m));
        document.Statement.ReserveFund.Should().BeNull();
        document.Statement.Total.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_TheStatementListsEveryExpenseOfTheMonth_ByGroupWithSubtotalsAndTheReserveFund()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup doorman = kit.NewGroup("Portaria");
        ExpenseGroup cleaning = kit.NewGroup("Limpeza");
        kit.NewEntry(kit.NewFixed(doorman, month, 700m, "Porteiro"), month);
        kit.NewEntry(kit.NewFixed(cleaning, month, 300m, "Faxineira"), month);
        kit.NewEntry(kit.NewSingle(cleaning, month, 120m, "Produtos de limpeza"), month);
        UnitCharge charge = kit.CloseMonth(month, options: OptionsOf(kit, [doorman, cleaning], [doorman], 10m)).Charges.Single(candidate => candidate.Number == "101");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        InvoiceStatementGroup[] groups = [.. document.Statement.Groups];
        groups.Select(group => group.Name).Should().Equal("Limpeza", "Portaria");
        groups[0].Items.Select(item => (item.Description, item.Amount)).Should().Equal(("Faxineira", 300m), ("Produtos de limpeza", 120m));
        groups[0].Subtotal.Should().Be(420m);
        groups[1].Items.Select(item => (item.Description, item.Amount)).Should().Equal(("Porteiro", 700m));
        groups[1].Subtotal.Should().Be(700m);
        document.Statement.ReserveFund.Should().Be(new InvoiceReserveFund(70m, 10m, 700m), "o fundo leva o percentual e a base de quando o mês fechou");
        document.Statement.Total.Should().Be(1190m);
    }

    [Fact]
    public async Task Handle_AnInstallmentShowsItsNumber()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup garages = kit.NewGroup("Manutenção das garagens");
        kit.NewEntry(kit.NewInstallment(garages, month, 600m, 6, 4, "Portão da garagem"), month);
        UnitCharge charge = kit.CloseMonth(month, options: OptionsOf(kit, [garages], [], 0m)).Charges.Single(candidate => candidate.Number == "101");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Groups.Single().Items.Select(item => (item.Description, item.Amount)).Should().Equal(("Portão da garagem (4/6)", 100m));
    }

    [Fact]
    public async Task Handle_TheItemsOfAGroupAreInAlphabeticalOrder()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup group = kit.NewGroup("Manutenção");
        kit.NewEntry(kit.NewSingle(group, month, 10m, "Zincagem"), month);
        kit.NewEntry(kit.NewSingle(group, month, 20m, "Áreas comuns"), month);
        kit.NewEntry(kit.NewSingle(group, month, 30m, "Bomba"), month);
        UnitCharge charge = kit.CloseMonth(month, options: OptionsOf(kit, [group], [], 0m)).Charges.Single(candidate => candidate.Number == "101");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Groups.Single().Items.Select(item => item.Description).Should().Equal("Áreas comuns", "Bomba", "Zincagem");
    }

    [Fact]
    public async Task Handle_EachItemCarriesTheSupplierAndTheNoteOfItsEntry()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup group = kit.NewGroup("Manutenção");
        kit.NewEntry(kit.NewFixed(group, month, 100m, "Portaria", supplier: "Segurança Ltda"), month).SetNote("OS 4521");
        kit.NewEntry(kit.NewFixed(group, month, 100m, "Jardinagem", supplier: "Verde Vida"), month);
        kit.NewEntry(kit.NewFixed(group, month, 100m, "Água"), month).SetNote("leitura de 01 a 30/10");
        kit.NewEntry(kit.NewFixed(group, month, 100m, "Zeladoria"), month);
        UnitCharge charge = kit.CloseMonth(month, options: OptionsOf(kit, [group], [], 0m)).Charges.Single(candidate => candidate.Number == "101");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Groups.Single().Items.Select(item => (item.Description, item.Detail)).Should().Equal(
            ("Água", "leitura de 01 a 30/10"),
            ("Jardinagem", "Verde Vida"),
            ("Portaria", "Segurança Ltda · OS 4521"),
            ("Zeladoria", null));
    }

    [Fact]
    public async Task Handle_ACanceledExpenseAndTheExpensesOfOtherMonthsAreNotListed()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup group = kit.NewGroup("Manutenção");
        kit.NewEntry(kit.NewSingle(group, month, 100m, "Conserto do portão"), month);
        ExpenseEntry canceled = kit.NewEntry(kit.NewSingle(group, month, 999m, "Serviço cancelado"), month);
        canceled.SetStatus(EntryStatus.Canceled).IsSuccess.Should().BeTrue();
        kit.NewEntry(kit.NewSingle(group, FinanceTestKit.M(2026, 9), 888m, "Serviço de setembro"), FinanceTestKit.M(2026, 9));
        UnitCharge charge = kit.CloseMonth(month, options: OptionsOf(kit, [group], [], 0m)).Charges.Single(candidate => candidate.Number == "101");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Groups.Single().Items.Select(item => item.Description).Should().Equal("Conserto do portão");
        document.Statement.Total.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_TheStatementTotalIsTheSumOfTheClosing_ReserveFundIncluded()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup group = kit.NewGroup("Portaria");
        kit.NewEntry(kit.NewFixed(group, month, 700m, "Porteiro"), month);
        kit.NewEntry(kit.NewFixed(group, month, 300m, "Faxineira"), month);
        MonthClosing closing = kit.CloseMonth(month, options: OptionsOf(kit, [group], [group], 15m));
        UnitCharge charge = closing.Charges.Single(candidate => candidate.Number == "102");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Total.Should().Be(closing.Total + 150m, "o total do mês só tem despesas, e o fundo de reserva (15% de 1.000) vem por cima");
    }

    [Fact]
    public async Task Handle_TheStatementLeavesTheIndividualConsumptionOut()
    {
        FinanceTestKit kit = new();
        YearMonth month = FinanceTestKit.M(2026, 10);
        ExpenseGroup doorman = kit.NewGroup("Portaria");
        kit.NewEntry(kit.NewFixed(doorman, month, 700m, "Porteiro"), month);
        ConsumptionTariff tariff = ConsumptionTariff.Create(
            kit.CondominiumId, month, TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(null, 0m, 8.63m)]).Value, null, 100m, null, FinanceTestKit.Now).Value;
        MeterReading[] readings =
        [
            MeterReading.Create(kit.CondominiumId, FakeApportionmentBasis.First.UnitId, month, ConsumptionService.Water, 100m, 112m, FinanceTestKit.Now).Value,
            MeterReading.Create(kit.CondominiumId, FakeApportionmentBasis.Second.UnitId, month, ConsumptionService.Water, 50m, 58m, FinanceTestKit.Now).Value,
        ];
        ClosingOptions options = new(new Dictionary<Guid, string> { [doorman.Id] = "Portaria" }, new HashSet<Guid>(), 0m, tariff, readings);
        UnitCharge charge = kit.CloseMonth(month, options: options).Charges.Single(candidate => candidate.Number == "101");
        charge.Lines.Select(line => line.Kind).Should().Contain(ChargeLineKind.Water, "a cobrança da unidade tem consumo, o demonstrativo não");

        InvoiceDocument document = await RenderedAsync(charge, kit);

        document.Statement.Groups.Select(group => group.Name).Should().Equal("Portaria");
        document.Statement.Total.Should().Be(700m);
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
