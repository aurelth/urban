using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class ContactEmailTests
{
    [Fact]
    public void Create_TrimsAndLowercases()
    {
        Result<ContactEmail> result = ContactEmail.Create("  Maria.Souza@Exemplo.COM ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("maria.souza@exemplo.com");
        result.Value.ToString().Should().Be("maria.souza@exemplo.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba.com")]
    [InlineData("@exemplo.com")]
    [InlineData("a@@exemplo.com")]
    [InlineData("a@b@exemplo.com")]
    [InlineData("a b@exemplo.com")]
    [InlineData("a@exemplo")]
    [InlineData("a@.exemplo.com")]
    [InlineData("a@exemplo.com.")]
    [InlineData("a@exemplo..com")]
    public void Create_WithAnInvalidAddress_Fails(string? address)
    {
        Result<ContactEmail> result = ContactEmail.Create(address);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ContactEmail.Invalid);
        ContactEmail.Invalid.Code.Should().Be("condominium.email.invalid");
    }

    [Fact]
    public void Create_AcceptsUpToTheMaximumLength_AndRejectsOneMore()
    {
        string local = new('a', ContactEmail.MaxLength - "@b.co".Length);

        ContactEmail.Create(local + "@b.co").IsSuccess.Should().BeTrue();
        ContactEmail.Create(local + "a@b.co").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Restore_KeepsTheStoredValueAsIs()
    {
        ContactEmail.Restore("guardado@exemplo.com").Value.Should().Be("guardado@exemplo.com");
    }
}
