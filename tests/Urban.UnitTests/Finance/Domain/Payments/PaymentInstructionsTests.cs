using FluentAssertions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

public class PaymentInstructionsTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(3);

    private static PixKey Pix(string key = "12345678000195") => PixKey.Create(PixKeyKind.Cnpj, key).Value;

    private static BankAccount Account() => BankAccount.Create("Banco do Brasil", "1234-5", "12345-6", BankAccountKind.Checking).Value;

    [Fact]
    public void Create_StoresThePixKeyAndTheAccountForTheCondominium()
    {
        Result<PaymentInstructions> result = PaymentInstructions.Create(CondominiumId, Pix(), Account(), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.CondominiumId.Should().Be(CondominiumId);
        result.Value.Pix.Should().Be(Pix());
        result.Value.Account.Should().Be(Account());
        result.Value.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_AcceptsOnlyThePixKeyOnlyTheAccountOrNeither()
    {
        PaymentInstructions.Create(CondominiumId, Pix(), null, Now).IsSuccess.Should().BeTrue();
        PaymentInstructions.Create(CondominiumId, null, Account(), Now).IsSuccess.Should().BeTrue();
        PaymentInstructions.Create(CondominiumId, null, null, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutACondominium_Fails()
    {
        PaymentInstructions.Create(Guid.Empty, Pix(), Account(), Now).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public void Replace_SwapsEverythingAndMovesTheUpdateInstant()
    {
        PaymentInstructions instructions = PaymentInstructions.Create(CondominiumId, Pix(), Account(), Now).Value;
        PixKey newKey = PixKey.Create(PixKeyKind.Email, "financeiro@condominio.com").Value;

        instructions.Replace(newKey, null, Later);

        instructions.Pix.Should().Be(newKey);
        instructions.Account.Should().BeNull();
        instructions.UpdatedAt.Should().Be(Later);
        instructions.CondominiumId.Should().Be(CondominiumId);
    }

    [Fact]
    public void Replace_WithNothingClearsTheInstructions()
    {
        PaymentInstructions instructions = PaymentInstructions.Create(CondominiumId, Pix(), Account(), Now).Value;

        instructions.Replace(null, null, Later);

        instructions.Pix.Should().BeNull();
        instructions.Account.Should().BeNull();
    }
}
