using FluentAssertions;
using Hangfire;
using Hangfire.PostgreSql;
using Urban.BackgroundJobs;

namespace Urban.UnitTests.BackgroundJobs;

public class BackgroundJobsOptionsTests
{
    [Fact]
    public void ByDefault_ProcessesJobsInThisProcessWithTwoWorkers()
    {
        BackgroundJobsOptions options = new();

        options.RunServer.Should().BeTrue();
        options.WorkerCount.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public void Validate_AcceptsAWorkerCountFromOneToTwenty(int workers)
    {
        Action validate = () => new BackgroundJobsOptions { WorkerCount = workers }.Validate();

        validate.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_RefusesAWorkerCountOutsideTheRange(int workers)
    {
        Action validate = () => new BackgroundJobsOptions { WorkerCount = workers }.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*BackgroundJobs:WorkerCount*");
    }

    [Fact]
    public void ApplyTo_SetsTheWorkerCountOnTheServer()
    {
        BackgroundJobServerOptions server = new();

        new BackgroundJobsOptions { WorkerCount = 5 }.ApplyTo(server);

        server.WorkerCount.Should().Be(5);
    }

    [Fact]
    public void CreateStorageOptions_KeepsTheQueueInItsOwnSchemaAndPollsOftenEnoughForConfirmationEmails()
    {
        PostgreSqlStorageOptions storage = BackgroundJobsOptions.CreateStorageOptions();

        storage.SchemaName.Should().Be("hangfire");
        storage.PrepareSchemaIfNecessary.Should().BeTrue();
        storage.QueuePollInterval.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateStorageOptions_FailsTheStartupWhenTheDatabaseIsUnreachable_InsteadOfRunningWithoutAQueue()
    {
        BackgroundJobsOptions.CreateStorageOptions().AllowDegradedModeWithoutStorage.Should().BeFalse();
    }
}