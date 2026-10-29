using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Urban.Condominium.Infrastructure.Security;

namespace Urban.UnitTests.Condominium.Infrastructure;

public class PersonalDataProtectorTests
{
    private const string Cpf = "52998224725";

    private static readonly string KeyV1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string KeyV2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static PersonalDataProtector Protector(string current = "v1", params (string Id, string Key)[] keys)
    {
        PersonalDataOptions options = new() { CurrentKeyId = current, HashKey = HashKey };
        foreach ((string id, string key) in keys.Length == 0 ? [("v1", KeyV1)] : keys)
        {
            options.EncryptionKeys[id] = key;
        }

        return new PersonalDataProtector(options);
    }

    [Fact]
    public void Protect_PutsTheCurrentKeyIdBeforeTheCiphertext()
    {
        Protector().Protect(Cpf).Should().StartWith("v1:");
    }

    [Theory]
    [InlineData(Cpf)]
    [InlineData("")]
    [InlineData("Ação com acentos e emoji 😀")]
    public void Unprotect_ReturnsWhatWasProtected(string plain)
    {
        PersonalDataProtector protector = Protector();

        protector.Unprotect(protector.Protect(plain)).Should().Be(plain);
    }

    [Fact]
    public void Protect_OfTheSameValueTwice_GivesDifferentResults_BecauseTheNonceIsRandom()
    {
        PersonalDataProtector protector = Protector();

        protector.Protect(Cpf).Should().NotBe(protector.Protect(Cpf));
    }

    [Theory]
    [InlineData(Cpf)]
    [InlineData("")]
    public void Protect_StoresNonce12Bytes_CipherOfTheSameLength_AndTag16Bytes(string plain)
    {
        string stored = Protector().Protect(plain);

        Convert.FromBase64String(stored[3..]).Should().HaveCount(12 + Encoding.UTF8.GetByteCount(plain) + 16);
    }

    [Fact]
    public void Protect_UsesTheCurrentKey_NotAnotherOneOfTheSet()
    {
        string stored = Protector("v1", ("v1", KeyV1), ("v2", KeyV2)).Protect(Cpf);

        stored.Should().StartWith("v1:");
        Protector("v1", ("v1", KeyV1)).Unprotect(stored).Should().Be(Cpf);
    }

    [Fact]
    public void Protect_DoesNotLeakThePlainValue()
    {
        string stored = Protector().Protect(Cpf);

        stored.Should().NotContain(Cpf);
        Encoding.UTF8.GetString(Convert.FromBase64String(stored[3..])).Should().NotContain(Cpf);
    }

    [Fact]
    public void Unprotect_OfAlteredCiphertext_Fails()
    {
        PersonalDataProtector protector = Protector();
        byte[] payload = Convert.FromBase64String(protector.Protect(Cpf)[3..]);
        payload[^1] ^= 0x01;

        Action read = () => protector.Unprotect("v1:" + Convert.ToBase64String(payload));

        read.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1:")]
    [InlineData("v1:AAAA")]
    [InlineData("v1:not base64 !!")]
    [InlineData("v9:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Unprotect_OfSomethingThatIsNotAValueItProduced_Fails(string stored)
    {
        Action read = () => Protector().Unprotect(stored);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WithAnotherKeyUnderTheSameIdentifier_Fails()
    {
        string stored = Protector().Protect(Cpf);

        Action read = () => Protector("v1", ("v1", KeyV2)).Unprotect(stored);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AfterARotation_ValuesOfTheOldKeyAreStillReadable_AndNewOnesUseTheNewKey()
    {
        string old = Protector().Protect(Cpf);
        PersonalDataProtector rotated = Protector("v2", ("v1", KeyV1), ("v2", KeyV2));

        rotated.Unprotect(old).Should().Be(Cpf);
        string fresh = rotated.Protect(Cpf);
        fresh.Should().StartWith("v2:");
        rotated.Unprotect(fresh).Should().Be(Cpf);
    }

    [Fact]
    public void AValueOfANewerKey_CannotBeReadWithoutIt()
    {
        string fresh = Protector("v2", ("v1", KeyV1), ("v2", KeyV2)).Protect(Cpf);

        Action read = () => Protector().Unprotect(fresh);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Hash_IsLowercaseHexOf64Characters_AndStable()
    {
        PersonalDataProtector protector = Protector();

        string first = protector.Hash(Cpf);

        first.Should().MatchRegex("^[0-9a-f]{64}$");
        protector.Hash(Cpf).Should().Be(first);
    }

    [Fact]
    public void Hash_IsHmacSha256OfTheUtf8ValueWithTheHashKey()
    {
        string expected = Convert.ToHexStringLower(HMACSHA256.HashData(Convert.FromBase64String(HashKey), Encoding.UTF8.GetBytes(Cpf)));

        Protector().Hash(Cpf).Should().Be(expected);
    }

    [Fact]
    public void Hash_DiffersForDifferentValues_AndForDifferentKeys()
    {
        PersonalDataProtector protector = Protector();
        PersonalDataOptions other = new() { CurrentKeyId = "v1", HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        other.EncryptionKeys["v1"] = KeyV1;

        protector.Hash(Cpf).Should().NotBe(protector.Hash("52998224726"));
        protector.Hash(Cpf).Should().NotBe(new PersonalDataProtector(other).Hash(Cpf));
    }

    [Fact]
    public void Hash_IsNotAPlainSha256_SoNobodyCanRebuildTheTableOfAllCpfsWithoutTheKey()
    {
        string plainSha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Cpf)));

        Protector().Hash(Cpf).Should().NotBe(plainSha);
    }

    [Fact]
    public void ThePrefixSeparatesTheKeyIdFromThePayload_EvenWhenTheIdentifierLooksLikeBase64()
    {
        PersonalDataProtector protector = Protector("a1", ("a1", KeyV1));

        protector.Unprotect(protector.Protect(Cpf)).Should().Be(Cpf);
    }
}
