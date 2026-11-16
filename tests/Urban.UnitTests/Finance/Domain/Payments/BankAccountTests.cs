using FluentAssertions;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

public class BankAccountTests
{
    [Fact]
    public void Create_KeepsTheBankTheAgencyTheAccountAndItsKind()
    {
        Result<BankAccount> result = BankAccount.Create("Banco do Brasil", "1234-5", "12345-6", BankAccountKind.Checking);

        result.IsSuccess.Should().BeTrue();
        result.Value.Bank.Should().Be("Banco do Brasil");
        result.Value.Agency.Should().Be("1234-5");
        result.Value.Number.Should().Be("12345-6");
        result.Value.Kind.Should().Be(BankAccountKind.Checking);
    }

    [Fact]
    public void Create_AcceptsASavingsAccountAndAgenciesAndAccountsWithoutCheckDigit()
    {
        Result<BankAccount> result = BankAccount.Create("Caixa", "0001", "00012345", BankAccountKind.Savings);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(BankAccountKind.Savings);
    }

    [Fact]
    public void Create_NormalizesTheSpacesInTheBankNameAndTheCheckDigitLetter()
    {
        BankAccount account = BankAccount.Create("  Caixa   Econômica  Federal ", " 123-x ", " 000123456-x ", BankAccountKind.Checking).Value;

        account.Bank.Should().Be("Caixa Econômica Federal");
        account.Agency.Should().Be("123-X");
        account.Number.Should().Be("000123456-X");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Banco\nInter")]
    public void Create_ABankNameNeedsTwoCharactersAndOneLine(string? bank)
    {
        BankAccount.Create(bank, "1234", "12345-6", BankAccountKind.Checking).Error.Should().Be(PaymentInstructionsErrors.BankInvalid);
    }

    [Fact]
    public void Create_ABankNameHasAtMostSixtyCharacters()
    {
        BankAccount.Create(new string('B', 60), "1234", "12345-6", BankAccountKind.Checking).IsSuccess.Should().BeTrue();
        BankAccount.Create(new string('B', 61), "1234", "12345-6", BankAccountKind.Checking).Error.Should().Be(PaymentInstructionsErrors.BankInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData("123456")]
    [InlineData("1234-")]
    [InlineData("1234-55")]
    [InlineData("12 34")]
    public void Create_AnAgencyIsUpToFiveDigitsWithAnOptionalCheckDigit(string? agency)
    {
        BankAccount.Create("Itaú", agency, "12345-6", BankAccountKind.Checking).Error.Should().Be(PaymentInstructionsErrors.AgencyInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1234567890123")]
    [InlineData("123-")]
    [InlineData("12 3")]
    [InlineData("123-45")]
    public void Create_AnAccountIsUpToTwelveDigitsWithAnOptionalCheckDigit(string? number)
    {
        BankAccount.Create("Itaú", "1234", number, BankAccountKind.Checking).Error.Should().Be(PaymentInstructionsErrors.NumberInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Create_AKindOutsideTheListIsRefused(int kind)
    {
        BankAccount.Create("Itaú", "1234", "12345-6", (BankAccountKind)kind).Error.Should().Be(PaymentInstructionsErrors.AccountKindInvalid);
    }

    [Fact]
    public void Create_ReportsTheFirstProblemInTheOrderBankAgencyAccountKind()
    {
        BankAccount.Create("", "x", "y", 0).Error.Should().Be(PaymentInstructionsErrors.BankInvalid);
        BankAccount.Create("Itaú", "x", "y", 0).Error.Should().Be(PaymentInstructionsErrors.AgencyInvalid);
        BankAccount.Create("Itaú", "1234", "y", 0).Error.Should().Be(PaymentInstructionsErrors.NumberInvalid);
        BankAccount.Create("Itaú", "1234", "12345-6", 0).Error.Should().Be(PaymentInstructionsErrors.AccountKindInvalid);
    }
}
