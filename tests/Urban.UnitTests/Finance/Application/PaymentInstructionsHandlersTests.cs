using FluentAssertions;
using Urban.Finance.Application.Instructions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class PaymentInstructionsHandlersTests
{
    private readonly FinanceTestKit _kit = new();

    private static readonly PixKeyInput CnpjKey = new(PixKeyKind.Cnpj, "12.345.678/0001-95");
    private static readonly BankAccountInput BankData = new("Banco do Brasil", "1234-5", "12345-6", BankAccountKind.Checking);

    private GetPaymentInstructionsQueryHandler GetHandler() => new(_kit.Instructions, _kit.Tenant);

    private SavePaymentInstructionsCommandHandler SaveHandler() => new(_kit.Instructions, _kit.Tenant, _kit.Clock);

    private PaymentInstructions Existing(PixKey? pix, BankAccount? account, DateTimeOffset? at = null) =>
        _kit.Instructions.Existing(PaymentInstructions.Create(_kit.CondominiumId, pix, account, at ?? FinanceTestKit.Now).Value);

    [Fact]
    public async Task Get_WhenNothingWasRegistered_ReturnsAnEmptyView()
    {
        Result<PaymentInstructionsView> result = await GetHandler().Handle(new GetPaymentInstructionsQuery(), default);

        result.Value.Pix.Should().BeNull();
        result.Value.Account.Should().BeNull();
        result.Value.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Get_ReturnsWhatWasRegistered()
    {
        Existing(PixKey.Create(PixKeyKind.Email, "financeiro@condominio.com").Value, BankAccount.Create("Itaú", "0001", "98765-4", BankAccountKind.Savings).Value);

        PaymentInstructionsView view = (await GetHandler().Handle(new GetPaymentInstructionsQuery(), default)).Value;

        view.Pix.Should().Be(new PixKeyView(PixKeyKind.Email, "financeiro@condominio.com"));
        view.Account.Should().Be(new BankAccountView("Itaú", "0001", "98765-4", BankAccountKind.Savings));
        view.UpdatedAt.Should().Be(FinanceTestKit.Now);
    }

    [Fact]
    public async Task Get_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await GetHandler().Handle(new GetPaymentInstructionsQuery(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Save_TheFirstTime_CreatesTheInstructionsForTheCondominiumWithNormalizedValues()
    {
        Result<PaymentInstructionsView> result = await SaveHandler().Handle(new SavePaymentInstructionsCommand(CnpjKey, BankData), default);

        result.Value.Pix.Should().Be(new PixKeyView(PixKeyKind.Cnpj, "12345678000195"));
        result.Value.Account.Should().Be(new BankAccountView("Banco do Brasil", "1234-5", "12345-6", BankAccountKind.Checking));
        result.Value.UpdatedAt.Should().Be(FinanceTestKit.Now);
        _kit.Instructions.Stored!.CondominiumId.Should().Be(_kit.CondominiumId);
        _kit.Instructions.Saves.Should().Be(1);
    }

    [Fact]
    public async Task Save_WhenItAlreadyExists_ChangesTheSameInstructions()
    {
        PaymentInstructions existing = Existing(PixKey.Create(PixKeyKind.Cpf, "12345678909").Value, null, FinanceTestKit.Now.AddDays(-30));

        Result<PaymentInstructionsView> result = await SaveHandler().Handle(new SavePaymentInstructionsCommand(null, BankData), default);

        result.Value.Pix.Should().BeNull();
        result.Value.Account!.Bank.Should().Be("Banco do Brasil");
        _kit.Instructions.Stored.Should().BeSameAs(existing);
        existing.UpdatedAt.Should().Be(FinanceTestKit.Now);
    }

    [Fact]
    public async Task Save_WithNothing_ClearsWhatWasRegistered()
    {
        Existing(PixKey.Create(PixKeyKind.Cpf, "12345678909").Value, BankAccount.Create("Itaú", "0001", "98765-4", BankAccountKind.Savings).Value);

        Result<PaymentInstructionsView> result = await SaveHandler().Handle(new SavePaymentInstructionsCommand(null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pix.Should().BeNull();
        result.Value.Account.Should().BeNull();
        _kit.Instructions.Saves.Should().Be(1);
    }

    [Fact]
    public async Task Save_WithAnInvalidPixKey_FailsAndSavesNothing()
    {
        Result<PaymentInstructionsView> result = await SaveHandler().Handle(new SavePaymentInstructionsCommand(new PixKeyInput(PixKeyKind.Cpf, "123"), BankData), default);

        result.Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
        _kit.Instructions.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Save_WithAnUnknownPixKind_Fails()
    {
        Result<PaymentInstructionsView> result = await SaveHandler().Handle(new SavePaymentInstructionsCommand(new PixKeyInput((PixKeyKind)(-1), "12345678909"), null), default);

        result.Error.Should().Be(PaymentInstructionsErrors.PixKeyKindInvalid);
    }

    [Fact]
    public async Task Save_WithAnInvalidAccount_FailsAndKeepsWhatWasRegistered()
    {
        PaymentInstructions existing = Existing(PixKey.Create(PixKeyKind.Cpf, "12345678909").Value, null);

        Result<PaymentInstructionsView> result = await SaveHandler().Handle(
            new SavePaymentInstructionsCommand(CnpjKey, new BankAccountInput("Itaú", "abcd", "12345-6", BankAccountKind.Checking)), default);

        result.Error.Should().Be(PaymentInstructionsErrors.AgencyInvalid);
        existing.Pix!.Key.Should().Be("12345678909");
        _kit.Instructions.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Save_ReportsThePixProblemBeforeTheAccountProblem()
    {
        Result<PaymentInstructionsView> result = await SaveHandler().Handle(
            new SavePaymentInstructionsCommand(new PixKeyInput(PixKeyKind.Cpf, "123"), new BankAccountInput("", "", "", 0)), default);

        result.Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Fact]
    public async Task Save_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await SaveHandler().Handle(new SavePaymentInstructionsCommand(CnpjKey, BankData), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Save_WhenSavingFails_ReturnsTheFailure()
    {
        _kit.Instructions.SaveFailure = FinanceErrors.ConcurrencyConflict;

        (await SaveHandler().Handle(new SavePaymentInstructionsCommand(CnpjKey, BankData), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }
}
