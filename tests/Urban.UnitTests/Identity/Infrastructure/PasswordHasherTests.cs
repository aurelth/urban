using FluentAssertions;
using Urban.Identity.Infrastructure.Security;

namespace Urban.UnitTests.Identity.Infrastructure;

public class PasswordHasherTests
{
    private const string Password = "Senha-forte-123";

    private readonly PasswordHasher _hasher = new(new PasswordHashingOptions { WorkFactor = 4 });

    [Fact]
    public void Verify_AcceptsThePasswordThatWasHashed()
    {
        string hash = _hasher.Hash(Password);

        _hasher.Verify(Password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsAnotherPassword()
    {
        string hash = _hasher.Hash(Password);

        _hasher.Verify("Outra-senha-456", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ProducesADifferentHashEachTimeBecauseOfTheSalt()
    {
        _hasher.Hash(Password).Should().NotBe(_hasher.Hash(Password));
    }

    [Fact]
    public void Hash_NeverContainsThePasswordItself()
    {
        _hasher.Hash(Password).Should().NotContain(Password);
    }

    [Fact]
    public void Hash_UsesTheConfiguredWorkFactor()
    {
        PasswordHasher slower = new(new PasswordHashingOptions { WorkFactor = 5 });

        _hasher.Hash(Password).Should().StartWith("$2a$04$");
        slower.Hash(Password).Should().StartWith("$2a$05$");
    }

    [Fact]
    public void Verify_NotesEveryCharacterEvenBeyondTheBcryptLimitOf72Bytes()
    {
        string prefix = new('a', 80);
        string hash = _hasher.Hash(prefix + "1");

        _hasher.Verify(prefix + "1", hash).Should().BeTrue();
        _hasher.Verify(prefix + "2", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_HandlesTheLongestAllowedPasswordWithAccents()
    {
        string password = new string('ç', 127) + "9";
        string hash = _hasher.Hash(password);

        _hasher.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyDummy_DoesTheWorkOfARealCheckWithoutThrowing()
    {
        Action act = () => _hasher.VerifyDummy(Password);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void Constructor_RejectsAWorkFactorOutsideTheSupportedRange(int workFactor)
    {
        Action create = () => _ = new PasswordHasher(new PasswordHashingOptions { WorkFactor = workFactor });

        create.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*entre 4 e 31*");
    }

    [Fact]
    public void Options_DefaultToAWorkFactorOf12()
    {
        new PasswordHashingOptions().WorkFactor.Should().Be(12);
    }
}