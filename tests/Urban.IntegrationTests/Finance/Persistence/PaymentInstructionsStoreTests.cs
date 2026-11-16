using FluentAssertions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class PaymentInstructionsStoreTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private PaymentInstructions New(PixKey? pix, BankAccount? account, DateTimeOffset? at = null) =>
        PaymentInstructions.Create(_kit.CondominiumId, pix, account, at ?? FinanceStoreKit.Now).Value;

    private static PixKey Pix(PixKeyKind kind = PixKeyKind.Cnpj, string key = "12345678000195") => PixKey.Create(kind, key).Value;

    private static BankAccount Account() => BankAccount.Create("Banco do Brasil", "1234-5", "12345-6", BankAccountKind.Savings).Value;

    [Fact]
    public async Task Find_BeforeAnythingWasSaved_ReturnsNull()
    {
        (await _kit.Instructions().FindAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_TheKeyAndTheAccount_ThenFind_ReturnsBoth()
    {
        Result saved = await _kit.Instructions().SaveAsync(New(Pix(PixKeyKind.Email, "financeiro@condominio.com"), Account()), default);
        PaymentInstructions? found = await _kit.Instructions().FindAsync(default);

        saved.IsSuccess.Should().BeTrue();
        found.Should().NotBeNull();
        found!.CondominiumId.Should().Be(_kit.CondominiumId);
        found.Pix.Should().Be(Pix(PixKeyKind.Email, "financeiro@condominio.com"));
        found.Account.Should().Be(Account());
        found.Account!.Kind.Should().Be(BankAccountKind.Savings);
        found.UpdatedAt.Should().Be(FinanceStoreKit.Now);
    }

    [Fact]
    public async Task Save_OnlyTheKey_ReadsBackWithoutAnAccount()
    {
        await _kit.Instructions().SaveAsync(New(Pix(), null), default);

        PaymentInstructions found = (await _kit.Instructions().FindAsync(default))!;

        found.Pix.Should().Be(Pix());
        found.Account.Should().BeNull();
    }

    [Fact]
    public async Task Save_OnlyTheAccount_ReadsBackWithoutAKey()
    {
        await _kit.Instructions().SaveAsync(New(null, Account()), default);

        PaymentInstructions found = (await _kit.Instructions().FindAsync(default))!;

        found.Pix.Should().BeNull();
        found.Account.Should().Be(Account());
    }

    [Fact]
    public async Task Save_Nothing_ReadsBackWithNeither()
    {
        await _kit.Instructions().SaveAsync(New(null, null), default);

        PaymentInstructions found = (await _kit.Instructions().FindAsync(default))!;

        found.Pix.Should().BeNull();
        found.Account.Should().BeNull();
    }

    [Fact]
    public async Task Save_AChange_UpdatesTheSameRowAndCanRemoveTheKey()
    {
        await _kit.Instructions().SaveAsync(New(Pix(), Account()), default);
        PaymentInstructionsStore store = _kit.Instructions();
        PaymentInstructions loaded = (await store.FindAsync(default))!;

        loaded.Replace(null, Account(), FinanceStoreKit.Now.AddDays(1));
        Result saved = await store.SaveAsync(loaded, default);

        saved.IsSuccess.Should().BeTrue();
        PaymentInstructions found = (await _kit.Instructions().FindAsync(default))!;
        found.Pix.Should().BeNull();
        found.Account.Should().Be(Account());
        found.UpdatedAt.Should().Be(FinanceStoreKit.Now.AddDays(1));
    }

    [Fact]
    public async Task Find_ForAnotherCondominium_ReturnsNull()
    {
        await _kit.Instructions().SaveAsync(New(Pix(), Account()), default);

        (await _kit.Instructions(Guid.CreateVersion7()).FindAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_TwoRequestsCreatingItAtTheSameTime_LetsOneWinAndTheOtherGetsAConflict()
    {
        PaymentInstructionsStore first = _kit.Instructions();
        PaymentInstructionsStore second = _kit.Instructions();
        await first.FindAsync(default);
        await second.FindAsync(default);

        Result won = await first.SaveAsync(New(Pix(), null), default);
        Result lost = await second.SaveAsync(New(null, Account()), default);

        won.IsSuccess.Should().BeTrue();
        lost.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Instructions().FindAsync(default))!.Pix.Should().Be(Pix());
    }
}
