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
using Urban.TestSupport.Identity;

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

    [Fact]
    public async Task Replying_without_a_session_returns_401()
    {
        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(Guid.NewGuid()), accessToken: null, "Já corrigimos o CNPJ.");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replying_as_a_manager_returns_403_with_a_stable_code_and_sends_nothing()
    {
        User ana = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        string anaToken = await _client.SignInAsync(ana);
        Guid id = await SendAsync(anaToken, "Assunto", "Texto da mensagem.");

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), anaToken, "Já corrigimos o CNPJ.");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.contact.reply_forbidden");
        fixture.Factory.Emails.SentTo(ana.Email.Value).Should().BeEmpty();
        (await RepliesOfAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Replying_to_an_unknown_message_returns_404()
    {
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(Guid.NewGuid()), adminToken, "Já corrigimos o CNPJ.");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("identity.contact.message_not_found");
    }

    [Fact]
    public async Task Replying_without_a_form_returns_400()
    {
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, ReplyRoute(Guid.NewGuid()), adminToken, new { body = "Já corrigimos o CNPJ." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("request.invalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcd")]
    public async Task Replying_with_an_invalid_text_returns_400_and_sends_nothing(string text)
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, text);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.reply_body_invalid");
        fixture.Factory.Emails.SentTo(ana.Email.Value).Should().BeEmpty();
        (await RepliesOfAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Replying_with_files_but_without_the_text_field_returns_400()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, body: null, ("ata.pdf", SampleFiles.Pdf()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.reply_body_invalid");
        fixture.Factory.Emails.SentTo(ana.Email.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task Replying_with_a_type_that_is_not_allowed_returns_400_and_sends_nothing()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Segue o programa.", ("programa.exe", SampleFiles.Pdf()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.attachment_type_not_allowed");
        fixture.Factory.Emails.SentTo(ana.Email.Value).Should().BeEmpty();
        (await RepliesOfAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Replying_with_a_file_whose_content_is_not_what_the_extension_says_returns_400()
    {
        (_, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Segue o documento.", ("ata.pdf", SampleFiles.Png()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.attachment_content_invalid");
    }

    [Fact]
    public async Task Replying_with_more_than_three_files_returns_400()
    {
        (_, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();
        (string, byte[])[] four = [.. Enumerable.Range(1, 4).Select(n => ($"ata-{n}.pdf", SampleFiles.Pdf()))];

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Quatro anexos.", four);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.contact.attachments_too_many");
    }

    [Fact]
    public async Task Replying_with_a_file_over_five_megabytes_returns_413()
    {
        (_, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();
        byte[] big = SampleFiles.PaddedTo(SampleFiles.Pdf(), ContactReplyAttachment.MaxFileBytes + 1);

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Um arquivo grande demais.", ("grande.pdf", big));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.CodeAsync()).Should().Be("identity.contact.attachment_too_large");
    }

    [Fact]
    public async Task Replying_with_files_that_together_pass_ten_megabytes_returns_413()
    {
        (_, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();
        byte[] four = SampleFiles.PaddedTo(SampleFiles.Pdf(), 4 * 1024 * 1024);

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Três arquivos grandes.", ("a.pdf", four), ("b.pdf", four), ("c.pdf", four));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.CodeAsync()).Should().Be("identity.contact.attachments_total_too_large");
    }

    [Fact]
    public async Task A_reply_goes_by_email_to_the_sender_with_the_admin_as_reply_to_and_the_files_attached()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync("Corrigir o CNPJ", "O CNPJ do condomínio está errado.");
        (User admin, string adminToken) = await SuperAdminAsync();
        byte[] pdf = SampleFiles.Pdf();
        byte[] png = SampleFiles.Png();

        HttpResponseMessage response = await _client.SendFormAsync(
            ReplyRoute(id),
            adminToken,
            "  Já corrigimos o CNPJ.\nConfira o comprovante.  ",
            ("comprovante.pdf", pdf),
            ("foto.png", png));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        EmailMessage sent = fixture.Factory.Emails.SentTo(ana.Email.Value).Single(m => m.Subject == "Re: Corrigir o CNPJ");
        sent.ReplyTo.Should().Be(admin.Email.Value);
        sent.TextBody.Should().Contain("Já corrigimos o CNPJ.\nConfira o comprovante.");
        sent.TextBody.Should().Contain("Em resposta à sua mensagem \"Corrigir o CNPJ\"");
        sent.Attachments!.Select(a => a.FileName).Should().Equal("comprovante.pdf", "foto.png");
        sent.Attachments![0].Content.Should().Equal(pdf);
        sent.Attachments[1].Content.Should().Equal(png);
    }

    [Fact]
    public async Task A_reply_returns_the_receipt_is_recorded_and_marks_the_message_as_read()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (User admin, string adminToken) = await SuperAdminAsync();
        byte[] pdf = SampleFiles.Pdf();

        HttpResponseMessage response = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Já corrigimos o CNPJ.", ("comprovante.pdf", pdf));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        ContactReply stored = (await RepliesOfAsync(id)).Single();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().Be(stored.Id);
        body.GetProperty("sentAt").GetDateTimeOffset().Should().BeCloseTo(stored.SentAt, TimeSpan.FromMilliseconds(1));
        JsonElement file = body.GetProperty("attachments").EnumerateArray().Single();
        file.GetProperty("fileName").GetString().Should().Be("comprovante.pdf");
        file.GetProperty("contentType").GetString().Should().Be("application/pdf");
        file.GetProperty("size").GetInt64().Should().Be(pdf.Length);
        file.TryGetProperty("content", out _).Should().BeFalse();
        stored.AuthorId.Should().Be(admin.Id);
        stored.Body.Should().Be("Já corrigimos o CNPJ.");
        stored.Attachments.Should().Equal(new ContactReplyAttachment("comprovante.pdf", "application/pdf", pdf.Length));
        (await MessagesOfAsync(ana.Id)).Single().ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Answering_twice_keeps_both_replies_and_the_first_reading()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();
        await _client.SendFormAsync(ReplyRoute(id), adminToken, "Primeira resposta.");
        DateTimeOffset firstReading = (await MessagesOfAsync(ana.Id)).Single().ReadAt!.Value;

        HttpResponseMessage second = await _client.SendFormAsync(ReplyRoute(id), adminToken, "Segunda resposta.");

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RepliesOfAsync(id)).Select(r => r.Body).Should().Equal("Primeira resposta.", "Segunda resposta.");
        (await MessagesOfAsync(ana.Id)).Single().ReadAt.Should().Be(firstReading);
    }

    [Fact]
    public async Task The_inbox_shows_each_reply_with_its_files_but_never_their_content()
    {
        (User ana, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();
        await _client.SendFormAsync(ReplyRoute(id), adminToken, "Primeira resposta.");
        await _client.SendFormAsync(ReplyRoute(id), adminToken, "Segunda resposta.", ("ata.pdf", SampleFiles.Pdf()));

        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?pageSize=100", adminToken)).Content.ReadFromJsonAsync<JsonElement>();

        JsonElement item = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        JsonElement[] replies = [.. item.GetProperty("replies").EnumerateArray()];
        replies.Select(r => r.GetProperty("body").GetString()).Should().Equal("Primeira resposta.", "Segunda resposta.");
        replies[0].GetProperty("attachments").GetArrayLength().Should().Be(0);
        replies[1].GetProperty("attachments")[0].GetProperty("fileName").GetString().Should().Be("ata.pdf");
        replies[1].TryGetProperty("sentAt", out _).Should().BeTrue();
        ana.Id.Should().Be(item.GetProperty("senderId").GetGuid());
    }

    [Fact]
    public async Task A_message_nobody_answered_shows_an_empty_list_of_replies()
    {
        (_, Guid id) = await ManagerWithMessageAsync();
        (_, string adminToken) = await SuperAdminAsync();

        JsonElement list = await (await _client.SendJsonAsync(HttpMethod.Get, $"{InboxRoute}?pageSize=100", adminToken)).Content.ReadFromJsonAsync<JsonElement>();

        list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id).GetProperty("replies").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task When_the_provider_refuses_the_email_the_reply_returns_502_and_nothing_is_recorded()
    {
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = fixture.RedisConnectionString,
        };
        await factory.Services.MigrateCondominiumAsync();
        using HttpClient client = factory.CreateClient(new() { HandleCookies = false });
        User ana = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");
        Guid id = (await (await client.SendJsonAsync(HttpMethod.Post, SendRoute, await client.SignInAsync(ana), Body("Assunto", "Texto da mensagem."))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        User admin = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Active, UserRole.SuperAdmin);
        string adminToken = await client.SignInAsync(admin);
        factory.Emails.RefuseDeliveries = true;

        HttpResponseMessage response = await client.SendFormAsync(ReplyRoute(id), adminToken, "Já corrigimos o CNPJ.");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await response.CodeAsync()).Should().Be("identity.contact.reply_not_delivered");
        (await RepliesOfAsync(id)).Should().BeEmpty();
        (await MessagesOfAsync(ana.Id)).Single().ReadAt.Should().BeNull();
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

    private static string ReplyRoute(Guid id) => $"{InboxRoute}/{id}/replies";

    private async Task<(User Manager, Guid MessageId)> ManagerWithMessageAsync(string subject = "Assunto", string body = "Texto da mensagem.")
    {
        User manager = await fixture.Postgres.SeedEnrolledManagerWithPasswordAsync(Password, "Ana");

        return (manager, await SendAsync(await _client.SignInAsync(manager), subject, body));
    }

    private async Task<(User Admin, string Token)> SuperAdminAsync()
    {
        User admin = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Active, UserRole.SuperAdmin);

        return (admin, await _client.SignInAsync(admin));
    }

    private async Task<ContactReply[]> RepliesOfAsync(Guid messageId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        return await db.ContactReplies.AsNoTracking().Where(r => r.MessageId == messageId).OrderBy(r => r.SentAt).ToArrayAsync();
    }

    private async Task<ContactMessage[]> MessagesOfAsync(Guid senderId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        return await db.ContactMessages.AsNoTracking().Where(m => m.SenderId == senderId).OrderBy(m => m.CreatedAt).ToArrayAsync();
    }
}
