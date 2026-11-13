using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class ContactEndpointsTests(ApiFixture fixture) : IAsyncLifetime, IDisposable
{
    private const string SendRoute = "/api/contact/messages";
    private const string InboxRoute = "/api/admin/contact-messages";
    private const string Password = "Senha-segura-123";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public Task InitializeAsync() => fixture.Factory.Services.MigrateCondominiumAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Sending_without_a_session_returns_401()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, accessToken: null, Body("Assunto", "Texto da mensagem."));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Sending_as_someone_who_is_not_a_manager_returns_403_and_stores_nothing(UserRole role)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Active, role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body("Assunto", "Texto da mensagem."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.contact.send_forbidden");
        (await MessagesOfAsync(user.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_manager_sends_a_message_and_gets_the_receipt()
    {
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body("  Corrigir o CNPJ ", " O CNPJ do condomínio está errado. "));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        ContactMessage stored = (await MessagesOfAsync(manager.Id)).Single();
        body.GetProperty("id").GetGuid().Should().Be(stored.Id);
        body.GetProperty("sentAt").GetDateTimeOffset().Should().BeCloseTo(stored.CreatedAt, TimeSpan.FromMilliseconds(1));
        stored.Subject.Should().Be("Corrigir o CNPJ");
        stored.Body.Should().Be("O CNPJ do condomínio está errado.");
        stored.CondominiumId.Should().Be(manager.CondominiumId);
        stored.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task A_manager_who_has_not_registered_the_condominium_yet_can_also_send()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body("Dúvida", "Como cadastro o condomínio?"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await MessagesOfAsync(manager.Id)).Single().CondominiumId.Should().BeNull();
    }

    [Theory]
    [InlineData("ab", "Texto da mensagem.", "identity.contact.subject_invalid")]
    [InlineData("", "Texto da mensagem.", "identity.contact.subject_invalid")]
    [InlineData("Assunto", "curta", "identity.contact.body_invalid")]
    [InlineData("Assunto", "", "identity.contact.body_invalid")]
    public async Task Sending_invalid_text_returns_400_with_the_code_of_the_field_and_stores_nothing(string subject, string body, string code)
    {
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body(subject, body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
        (await MessagesOfAsync(manager.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sending_without_a_body_returns_400()
    {
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_new_message_notifies_the_owner_by_email_with_the_sender_the_text_and_the_inbox_link()
    {
        string owner = NewAddress();
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = fixture.RedisConnectionString,
            SuperAdminEmail = owner,
        };
        await factory.Services.MigrateCondominiumAsync();
        using HttpClient client = factory.CreateClient(new() { HandleCookies = false });
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana Paula");
        string token = await client.SignInAsync(manager);

        HttpResponseMessage response = await client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body("Corrigir o CNPJ", "O CNPJ do condomínio está errado."));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        EmailMessage notice = factory.Emails.SentTo(owner).Single(m => m.Subject == "Nova mensagem de contato no Urban");
        notice.TextBody.Should().Contain("Ana Paula").And.Contain(manager.Email.Value);
        notice.TextBody.Should().Contain("Corrigir o CNPJ").And.Contain("O CNPJ do condomínio está errado.");
        notice.TextBody.Should().Contain("https://app.test/admin/mensagens");
        factory.Emails.SentTo(manager.Email.Value).Should().BeEmpty();
    }

    [Theory]
    [InlineData("get", InboxRoute)]
    [InlineData("post", InboxRoute + "/00000000-0000-0000-0000-000000000001/read")]
    public async Task Using_the_inbox_without_a_session_returns_401(string method, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("get", InboxRoute)]
    [InlineData("post", InboxRoute + "/00000000-0000-0000-0000-000000000001/read")]
    public async Task Using_the_inbox_as_a_manager_returns_403_with_a_stable_code(string method, string route)
    {
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.contact.read_forbidden");
    }

    [Fact]
    public async Task The_inbox_shows_the_messages_newest_first_with_the_sender_and_is_never_cached()
    {
        User ana = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string anaToken = await _client.SignInAsync(ana);
        Guid first = await SendAsync(anaToken, "Primeiro assunto", "Primeira mensagem do síndico.");
        Guid second = await SendAsync(anaToken, "Segundo assunto", "Segunda mensagem do síndico.");
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?pageSize=100", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(100);
        JsonElement[] mine = body.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("senderId").GetGuid() == ana.Id).ToArray();
        mine.Select(i => i.GetProperty("id").GetGuid()).Should().Equal(second, first);
        JsonElement item = mine[1];
        item.GetProperty("senderEmail").GetString().Should().Be(ana.Email.Value);
        item.GetProperty("senderName").GetString().Should().Be("Ana");
        item.GetProperty("subject").GetString().Should().Be("Primeiro assunto");
        item.GetProperty("body").GetString().Should().Be("Primeira mensagem do síndico.");
        item.GetProperty("readAt").ValueKind.Should().Be(JsonValueKind.Null);
        item.TryGetProperty("sentAt", out _).Should().BeTrue();
        item.TryGetProperty("condominiumName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task The_inbox_shows_the_real_name_of_the_condominium_of_the_sender()
    {
        ManagerSession session = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid id = await SendAsync(session.AccessToken, "Dúvida", "Como funciona o rateio?");
        string adminToken = await SuperAdminTokenAsync();

        JsonElement body = await (await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?pageSize=100", adminToken)).Content.ReadFromJsonAsync<JsonElement>();

        JsonElement item = body.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        item.GetProperty("condominiumName").GetString().Should().Be("Residencial Vila Verde");
    }

    [Fact]
    public async Task The_inbox_can_be_filtered_to_the_unread_ones()
    {
        User ana = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string anaToken = await _client.SignInAsync(ana);
        Guid read = await SendAsync(anaToken, "Já lida", "Mensagem que já foi lida.");
        Guid unread = await SendAsync(anaToken, "Nova", "Mensagem que ainda não foi lida.");
        string adminToken = await SuperAdminTokenAsync();
        (await _client.SendJsonAsync(HttpMethod.Post, $"{InboxRoute}/{read}/read", adminToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement body = await (await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?status=unread&pageSize=100", adminToken)).Content.ReadFromJsonAsync<JsonElement>();

        Guid[] ids = [.. body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
        ids.Should().Contain(unread).And.NotContain(read);
    }

    [Fact]
    public async Task Marking_as_read_records_when_it_was_read_and_is_safe_to_repeat()
    {
        User ana = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        Guid id = await SendAsync(await _client.SignInAsync(ana), "Assunto", "Texto da mensagem.");
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage first = await _client.SendJsonAsync(HttpMethod.Post, $"{InboxRoute}/{id}/read", adminToken);
        DateTimeOffset firstReading = (await MessagesOfAsync(ana.Id)).Single().ReadAt!.Value;
        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, $"{InboxRoute}/{id}/read", adminToken);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MessagesOfAsync(ana.Id)).Single().ReadAt.Should().Be(firstReading);
    }

    [Fact]
    public async Task Marking_an_unknown_message_as_read_returns_404()
    {
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{InboxRoute}/{Guid.NewGuid()}/read", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("identity.contact.message_not_found");
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Listing_with_an_invalid_page_returns_400(string query)
    {
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?{query}", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.page_invalid");
    }

    [Fact]
    public async Task Listing_with_an_unknown_status_returns_400()
    {
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?status=qualquer", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    private static object Body(string subject, string body) => new { subject, body };

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private async Task<Guid> SendAsync(string token, string subject, string body)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, SendRoute, token, Body(subject, body));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<string> SuperAdminTokenAsync()
    {
        User admin = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Active, UserRole.SuperAdmin);
        return await _client.SignInAsync(admin);
    }

    private async Task<ContactMessage[]> MessagesOfAsync(Guid senderId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        return await db.ContactMessages.AsNoTracking().Where(m => m.SenderId == senderId).OrderBy(m => m.CreatedAt).ToArrayAsync();
    }
}
