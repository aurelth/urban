using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>O PDF da cobrança de uma unidade. Hoje é 7/11/2026; outubro está fechado com duas cobranças, 600 (unidade 101) e 400 (bloco A, 102), vencendo em 10/11.</summary>
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

    private GetInvoiceQueryHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new GetInvoiceQueryHandler(used.Charges, used.Parties, used.Renderer, used.Tenant, used.Clock);
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
}
