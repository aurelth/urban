using FluentAssertions;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Payments;

public class PixKeyTests
{
    [Theory]
    [InlineData("12.345.678/0001-95", "12345678000195")]
    [InlineData(" 12345678000195 ", "12345678000195")]
    [InlineData("ab.cde.fgh/0001-23", "ABCDEFGH000123")]
    public void Create_ACnpjKeepsOnlyLettersAndDigitsInUpperCase(string typed, string expected)
    {
        Result<PixKey> result = PixKey.Create(PixKeyKind.Cnpj, typed);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(PixKeyKind.Cnpj);
        result.Value.Key.Should().Be(expected);
    }

    [Theory]
    [InlineData("1234567800019")]
    [InlineData("123456780001955")]
    [InlineData("ABCDEFGH0001AB")]
    [InlineData("1234567800019!")]
    public void Create_ACnpjNeedsFourteenCharactersWithTwoCheckDigits(string typed)
    {
        PixKey.Create(PixKeyKind.Cnpj, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Theory]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData(" 12345678909 ", "12345678909")]
    public void Create_ACpfKeepsOnlyTheDigits(string typed, string expected)
    {
        PixKey.Create(PixKeyKind.Cpf, typed).Value.Key.Should().Be(expected);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("123.456.789-0A")]
    public void Create_ACpfNeedsElevenDigits(string typed)
    {
        PixKey.Create(PixKeyKind.Cpf, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Fact]
    public void Create_AnEmailIsTrimmedAndLowercased()
    {
        PixKey.Create(PixKeyKind.Email, "  Sindico@Condominio.com.br ").Value.Key.Should().Be("sindico@condominio.com.br");
    }

    [Theory]
    [InlineData("sindico")]
    [InlineData("sindico@condominio")]
    [InlineData("sindico @condominio.com")]
    [InlineData("@condominio.com")]
    [InlineData("a\nb@condominio.com")]
    public void Create_AnEmailNeedsTheUsualShape(string typed)
    {
        PixKey.Create(PixKeyKind.Email, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Fact]
    public void Create_AnEmailHasAtMostSeventySevenCharacters()
    {
        PixKey.Create(PixKeyKind.Email, new string('a', 71) + "@x.com").IsSuccess.Should().BeTrue();
        PixKey.Create(PixKeyKind.Email, new string('a', 72) + "@x.com").Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Theory]
    [InlineData("(11) 91234-5678", "+5511912345678")]
    [InlineData("11912345678", "+5511912345678")]
    [InlineData("+55 (11) 91234-5678", "+5511912345678")]
    [InlineData("+5511912345678", "+5511912345678")]
    public void Create_APhoneIsStoredWithTheCountryCode(string typed, string expected)
    {
        PixKey.Create(PixKeyKind.Phone, typed).Value.Key.Should().Be(expected);
    }

    [Fact]
    public void Create_APhoneStartingWith55WithoutThePlusSignIsTheAreaCode55()
    {
        PixKey.Create(PixKeyKind.Phone, "55912345678").Value.Key.Should().Be("+5555912345678");
    }

    [Theory]
    [InlineData("(11) 3123-4567")]
    [InlineData("(01) 91234-5678")]
    [InlineData("(11) 81234-5678")]
    [InlineData("+55 11 9123")]
    [InlineData("11 9123x-5678")]
    public void Create_APhoneNeedsAnAreaCodeAndANineDigitMobileNumber(string typed)
    {
        PixKey.Create(PixKeyKind.Phone, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Fact]
    public void Create_ARandomKeyIsAnUuidInLowercase()
    {
        PixKey.Create(PixKeyKind.Random, " 123E4567-E89B-12D3-A456-426614174000 ").Value.Key.Should().Be("123e4567-e89b-12d3-a456-426614174000");
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("123e4567e89b12d3a456426614174000")]
    [InlineData("g23e4567-e89b-12d3-a456-426614174000")]
    [InlineData("123e4567-e89b-12d3-a456-42661417400")]
    public void Create_ARandomKeyNeedsTheCanonicalUuidForm(string typed)
    {
        PixKey.Create(PixKeyKind.Random, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ABlankKeyIsRefusedForEveryKind(string? typed)
    {
        foreach (PixKeyKind kind in Enum.GetValues<PixKeyKind>())
        {
            PixKey.Create(kind, typed).Error.Should().Be(PaymentInstructionsErrors.PixKeyInvalid);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_AKindOutsideTheListIsRefusedEvenWithAValidKey(int kind)
    {
        PixKey.Create((PixKeyKind)kind, "12345678909").Error.Should().Be(PaymentInstructionsErrors.PixKeyKindInvalid);
    }
}
