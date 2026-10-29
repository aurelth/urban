using System.Security.Cryptography;
using FluentAssertions;
using Urban.Condominium.Infrastructure.Security;

namespace Urban.UnitTests.Condominium.Infrastructure;

public class PersonalDataOptionsTests
{
    private static string Key(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    private static PersonalDataOptions Valid() => new()
    {
        CurrentKeyId = "v1",
        EncryptionKeys = { ["v1"] = Key() },
        HashKey = Key(),
    };

    [Fact]
    public void TheSectionIsCalledPersonalData()
    {
        PersonalDataOptions.SectionName.Should().Be("PersonalData");
    }

    [Fact]
    public void AValidConfigurationExposesTheKeysAsBytes()
    {
        PersonalDataOptions options = Valid();

        options.GetEncryptionKeys().Should().ContainKey("v1").WhoseValue.Should().HaveCount(32);
        options.GetHashKey().Should().HaveCountGreaterThanOrEqualTo(32);
    }

    [Fact]
    public void KeysFromARotationAreAllKept_AndTheCurrentOneMustBeAmongThem()
    {
        PersonalDataOptions options = new()
        {
            CurrentKeyId = "v2",
            EncryptionKeys = { ["v1"] = Key(), ["v2"] = Key() },
            HashKey = Key(),
        };

        options.GetEncryptionKeys().Keys.Should().BeEquivalentTo("v1", "v2");
    }

    [Fact]
    public void AKeyWithTrailingLineBreaks_StillCounts_BecauseSecretStoresOftenAddOne()
    {
        PersonalDataOptions options = new()
        {
            CurrentKeyId = "v1",
            EncryptionKeys = { ["v1"] = Key() + "\r\n" },
            HashKey = Key() + "\n",
        };

        options.GetEncryptionKeys()["v1"].Should().HaveCount(32);
        options.GetHashKey().Should().HaveCountGreaterThanOrEqualTo(32);
    }

    [Fact]
    public void WithoutAnyEncryptionKey_FailsNamingTheSetting()
    {
        PersonalDataOptions options = new() { CurrentKeyId = "v1", HashKey = Key() };

        Action read = () => options.GetEncryptionKeys();

        read.Should().Throw<InvalidOperationException>().WithMessage("Falta a configuração PersonalData:EncryptionKeys*");
    }

    [Fact]
    public void WhenTheCurrentKeyIsNotAmongTheKeys_Fails()
    {
        PersonalDataOptions options = Valid();

        Action read = () => new PersonalDataOptions
        {
            CurrentKeyId = "v9",
            EncryptionKeys = { ["v1"] = options.EncryptionKeys["v1"] },
            HashKey = options.HashKey,
        }.GetEncryptionKeys();

        read.Should().Throw<InvalidOperationException>().WithMessage("*PersonalData:CurrentKeyId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !!")]
    [InlineData("QUJD")]
    public void AnEncryptionKeyThatIsNotBase64Of32Bytes_Fails_WithoutRevealingIt(string key)
    {
        PersonalDataOptions options = Valid();
        options.EncryptionKeys["v1"] = key;

        Action read = () => options.GetEncryptionKeys();

        InvalidOperationException error = read.Should().Throw<InvalidOperationException>().WithMessage("*'v1'*32 bytes*").Which;
        if (key.Length > 0)
        {
            error.Message.Should().NotContain(key);
        }
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void AnEncryptionKeyThatIsNot32Bytes_Fails(int bytes)
    {
        PersonalDataOptions options = Valid();
        options.EncryptionKeys["v1"] = Key(bytes);

        Action read = () => options.GetEncryptionKeys();

        read.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("V1")]
    [InlineData("with space")]
    [InlineData("aaaaaaaaaaaaaaaaa")]
    public void AKeyIdentifierThatCouldBreakTheStoredFormat_Fails(string id)
    {
        PersonalDataOptions options = new()
        {
            CurrentKeyId = id,
            EncryptionKeys = { [id] = Key() },
            HashKey = Key(),
        };

        Action read = () => options.GetEncryptionKeys();

        read.Should().Throw<InvalidOperationException>().WithMessage("*identificador*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !!")]
    [InlineData("QUJD")]
    public void AHashKeyThatIsNotBase64OfAtLeast32Bytes_Fails_WithoutRevealingIt(string key)
    {
        PersonalDataOptions options = Valid();
        PersonalDataOptions broken = new() { CurrentKeyId = "v1", EncryptionKeys = { ["v1"] = options.EncryptionKeys["v1"] }, HashKey = key };

        Action read = () => broken.GetHashKey();

        InvalidOperationException error = read.Should().Throw<InvalidOperationException>().WithMessage("*PersonalData:HashKey*").Which;
        if (key.Length > 0)
        {
            error.Message.Should().NotContain(key);
        }
    }

    [Fact]
    public void TheHashKeyMustDifferFromEveryEncryptionKey()
    {
        string shared = Key();
        PersonalDataOptions options = new() { CurrentKeyId = "v1", EncryptionKeys = { ["v1"] = shared }, HashKey = shared };

        Action read = () => options.GetHashKey();

        read.Should().Throw<InvalidOperationException>().WithMessage("*diferente*");
    }

    [Fact]
    public void TheDefaultCurrentKeyIsV1()
    {
        new PersonalDataOptions().CurrentKeyId.Should().Be("v1");
    }
}
