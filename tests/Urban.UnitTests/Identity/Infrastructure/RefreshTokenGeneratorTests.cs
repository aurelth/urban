using System.Text.RegularExpressions;
using FluentAssertions;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure.Tokens;

namespace Urban.UnitTests.Identity.Infrastructure;

public class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsAUrlSafeSecretWith256BitsOfEntropy()
    {
        RefreshSecret secret = _generator.Generate();

        Regex.IsMatch(secret.Value, "^[A-Za-z0-9_-]{43}$").Should().BeTrue();
    }

    [Fact]
    public void Generate_NeverRepeatsASecret()
    {
        IEnumerable<string> values = Enumerable.Range(0, 100).Select(_ => _generator.Generate().Value);

        values.Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void Generate_ReturnsTheHashOfTheValueAndNeverTheValueItself()
    {
        RefreshSecret secret = _generator.Generate();

        secret.Hash.Should().Be(_generator.Hash(secret.Value));
        secret.Hash.Should().NotBe(secret.Value);
        Regex.IsMatch(secret.Hash, "^[0-9a-f]{64}$").Should().BeTrue();
    }

    [Fact]
    public void Hash_MatchesTheKnownSha256Vector()
    {
        _generator.Hash("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Hash_IsDeterministicAndSensitiveToTheInput()
    {
        _generator.Hash("a").Should().Be(_generator.Hash("a"));
        _generator.Hash("a").Should().NotBe(_generator.Hash("b"));
    }
}
