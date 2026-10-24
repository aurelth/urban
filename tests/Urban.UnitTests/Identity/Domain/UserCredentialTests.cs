using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class UserCredentialTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static UserCredential NewCredential() => UserCredential.Create(Guid.CreateVersion7(), "hash-de-teste");

    private static void Fail(UserCredential credential, int times)
    {
        for (int i = 0; i < times; i++)
        {
            credential.RegisterFailure(Now);
        }
    }

    [Fact]
    public void Create_StartsWithoutFailuresAndUnlocked()
    {
        Guid userId = Guid.CreateVersion7();

        UserCredential credential = UserCredential.Create(userId, "hash-de-teste");

        credential.UserId.Should().Be(userId);
        credential.PasswordHash.Should().Be("hash-de-teste");
        credential.FailedAttempts.Should().Be(0);
        credential.LockoutEnd.Should().BeNull();
        credential.IsLockedOut(Now).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankHash(string hash)
    {
        Action create = () => UserCredential.Create(Guid.CreateVersion7(), hash);

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RegisterFailure_BelowTheLimitCountsButDoesNotLock(int failures)
    {
        UserCredential credential = NewCredential();

        Fail(credential, failures);

        credential.FailedAttempts.Should().Be(failures);
        credential.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact]
    public void RegisterFailure_TheFifthLocksForFifteenMinutesAndRestartsTheCount()
    {
        UserCredential credential = NewCredential();

        Fail(credential, UserCredential.MaxFailedAttempts);

        credential.IsLockedOut(Now).Should().BeTrue();
        credential.LockoutEnd.Should().Be(Now.AddMinutes(15));
        credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void IsLockedOut_EndsExactlyWhenTheLockoutTimeIsOver()
    {
        UserCredential credential = NewCredential();
        Fail(credential, UserCredential.MaxFailedAttempts);
        DateTimeOffset end = credential.LockoutEnd!.Value;

        credential.IsLockedOut(end.AddTicks(-1)).Should().BeTrue();
        credential.IsLockedOut(end).Should().BeFalse();
    }

    [Fact]
    public void RegisterFailure_DuringALockoutDoesNotExtendIt()
    {
        UserCredential credential = NewCredential();
        Fail(credential, UserCredential.MaxFailedAttempts);
        DateTimeOffset end = credential.LockoutEnd!.Value;

        credential.RegisterFailure(Now.AddMinutes(5));

        credential.LockoutEnd.Should().Be(end);
        credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void RegisterFailure_AfterTheLockoutStartsANewCount()
    {
        UserCredential credential = NewCredential();
        Fail(credential, UserCredential.MaxFailedAttempts);

        credential.RegisterFailure(Now.AddMinutes(16));

        credential.FailedAttempts.Should().Be(1);
        credential.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse();
    }

    [Fact]
    public void RegisterSuccess_ClearsThePreviousFailures()
    {
        UserCredential credential = NewCredential();
        Fail(credential, 4);

        credential.RegisterSuccess();
        Fail(credential, 4);

        credential.IsLockedOut(Now).Should().BeFalse();
        credential.FailedAttempts.Should().Be(4);
    }

    [Fact]
    public void RegisterSuccess_ClearsTheLockoutDate()
    {
        UserCredential credential = NewCredential();
        Fail(credential, UserCredential.MaxFailedAttempts);

        credential.RegisterSuccess();

        credential.LockoutEnd.Should().BeNull();
        credential.FailedAttempts.Should().Be(0);
    }
}