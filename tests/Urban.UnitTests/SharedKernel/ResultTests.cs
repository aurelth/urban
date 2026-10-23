using FluentAssertions;
using Urban.SharedKernel;

namespace Urban.UnitTests.SharedKernel;

public class ResultTests
{
    private static readonly Error SampleError = new("sample.error", "Algo deu errado");

    [Fact]
    public void Success_HasNoError()
    {
        Result result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ExposesTheError()
    {
        Result result = Result.Failure(SampleError);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_WithNoneError_Throws()
    {
        Action act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SuccessOfT_ExposesTheValue()
    {
        Result<int> result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FailureOfT_AccessingTheValue_Throws()
    {
        Result<int> result = Result.Failure<int>(SampleError);

        Func<int> act = () => result.Value;

        result.Error.Should().Be(SampleError);
        act.Should().Throw<InvalidOperationException>();
    }
}
