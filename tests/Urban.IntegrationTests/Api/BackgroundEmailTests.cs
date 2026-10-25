using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class BackgroundEmailTests(ApiFixture fixture)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Register_answers_right_away_and_the_hangfire_server_sends_the_email_afterwards()
    {
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            RealBackgroundJobs = true,
        };
        using HttpClient client = factory.CreateClient();
        string address = $"{Guid.NewGuid():N}@example.com";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", new { email = address });
        EmailMessage[] sent = await WaitForEmailAsync(factory.Emails, address);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        sent.Should().ContainSingle().Which.TextBody.Should().Contain("https://app.test/confirmar-email?token=");
    }

    private static async Task<EmailMessage[]> WaitForEmailAsync(RecordingEmailSender emails, string address)
    {
        DateTime deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            EmailMessage[] sent = emails.SentTo(address);
            if (sent.Length > 0)
            {
                return sent;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        return [];
    }
}