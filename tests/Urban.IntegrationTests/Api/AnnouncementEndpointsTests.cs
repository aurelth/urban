using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Operations.Infrastructure;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// Os comunicados de ponta a ponta: o síndico escreve, edita, apaga e vê quantos leram; quem mora (proprietário, inquilino ou
/// dependente) lê o mural do que o alcança e marca como lido. Os moradores são reais (cadastro, convite, senha e entrada).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AnnouncementEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Route = "/api/announcements";
    private const string MineRoute = Route + "/mine";
    private const string ReadAllRoute = Route + "/read-all";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string ItemRoute(Guid id) => $"{Route}/{id}";

    private static string ReadRoute(Guid id) => $"{Route}/{id}/read";

    // ---- Acesso ----

    [Theory]
    [InlineData("GET", Route)]
    [InlineData("POST", Route)]
    [InlineData("PUT", "/api/announcements/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/announcements/00000000-0000-0000-0000-000000000001?version=1")]
    [InlineData("GET", MineRoute)]
    [InlineData("POST", "/api/announcements/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("POST", ReadAllRoute)]
    public async Task Every_route_requires_a_token(string method, string route)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, null, method is "POST" or "PUT" ? Body() : null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_resident_cannot_write_nor_list_the_announcements_of_the_manager()
    {
        (ManagerSession manager, Guid unit) = await ManagerWithAUnitAsync();
        string resident = await ResidentTokenAsync(manager, unit);
        Guid existing = await CreateAsync(manager);

        HttpResponseMessage[] responses =
        [
            await _client.SendJsonAsync(HttpMethod.Get, Route, resident),
            await _client.SendJsonAsync(HttpMethod.Post, Route, resident, Body()),
            await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(existing), resident, Body(version: "1")),
            await _client.SendJsonAsync(HttpMethod.Delete, $"{ItemRoute(existing)}?version=1", resident),
        ];

        foreach (HttpResponseMessage response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.CodeAsync()).Should().Be("identity.user.not_manager");
        }
    }

    [Fact]
    public async Task The_super_admin_has_no_condominium_and_is_refused_everywhere()
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, AccountStatus.Active, UserRole.SuperAdmin);
        string token = await _client.SignInAsync(user);

        (await _client.SendJsonAsync(HttpMethod.Get, Route, token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage mine = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, token);
        mine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mine.CodeAsync()).Should().Be("identity.user.not_condominium_member");
        (await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(Guid.NewGuid()), token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage readAll = await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, token);
        readAll.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await readAll.CodeAsync()).Should().Be("identity.user.not_condominium_member");
    }

    [Fact]
    public async Task A_manager_who_has_not_registered_the_condominium_yet_gets_not_found()
    {
        await MigrateAsync();
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(manager);

        HttpResponseMessage list = await _client.SendJsonAsync(HttpMethod.Get, Route, token);
        HttpResponseMessage create = await _client.SendJsonAsync(HttpMethod.Post, Route, token, Body());

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await list.CodeAsync()).Should().Be("operations.condominium_not_found");
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Criar ----

    [Fact]
    public async Task Create_ForTheWholeCondominium_Returns201_WithTheAnnouncementItsVersionAndTheLocation()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Corte de água", "Sábado, das 8h às 12h.\nSem água no prédio.", "maintenance"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Guid id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.OriginalString.Should().Be(ItemRoute(id));
        body.GetProperty("title").GetString().Should().Be("Corte de água");
        body.GetProperty("body").GetString().Should().Be("Sábado, das 8h às 12h.\nSem água no prédio.");
        body.GetProperty("category").GetString().Should().Be("maintenance");
        body.GetProperty("scope").GetString().Should().Be("condominium");
        body.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("unitId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("recipients").GetInt32().Should().Be(0);
        body.GetProperty("readers").GetInt32().Should().Be(0);
        body.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Create_ForABlockOrAUnit_KeepsTheScope()
    {
        (ManagerSession manager, Guid unit) = await ManagerWithAUnitAsync();

        JsonElement block = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(scope: "block", block: "Bloco A")));
        JsonElement single = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(scope: "unit", unitId: unit)));

        block.GetProperty("scope").GetString().Should().Be("block");
        block.GetProperty("block").GetString().Should().Be("Bloco A");
        block.GetProperty("unitId").ValueKind.Should().Be(JsonValueKind.Null);
        single.GetProperty("scope").GetString().Should().Be("unit");
        single.GetProperty("unitId").GetGuid().Should().Be(unit);
        single.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Create_ABlockWithNoUnits_Or_AUnitOfAnotherCondominium_Is422()
    {
        (ManagerSession manager, _) = await ManagerWithAUnitAsync();
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid strangersUnit = await CreateUnitAsync(stranger, "Bloco A", "101");

        HttpResponseMessage block = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(scope: "block", block: "Bloco Z"));
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(scope: "unit", unitId: Guid.NewGuid()));
        HttpResponseMessage foreign = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(scope: "unit", unitId: strangersUnit));

        block.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await block.CodeAsync()).Should().Be("announcement.block.not_found");
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await unknown.CodeAsync()).Should().Be("announcement.unit.not_found");
        foreign.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await foreign.CodeAsync()).Should().Be("announcement.unit.not_found");
        (await ListAsync(manager)).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("", "Texto", "general", "condominium", "announcement.title.invalid")]
    [InlineData("Duas\nlinhas", "Texto", "general", "condominium", "announcement.title.invalid")]
    [InlineData("Título", "", "general", "condominium", "announcement.body.invalid")]
    [InlineData("Título", "Texto", "urgent", "condominium", "announcement.category.invalid")]
    [InlineData("Título", "Texto", "general", "everyone", "announcement.scope.invalid")]
    [InlineData("Título", "Texto", "general", "block", "announcement.block.invalid")]
    [InlineData("Título", "Texto", "general", "unit", "announcement.unit.required")]
    public async Task Create_InvalidData_Is400_WithTheCodeOfTheField(string title, string text, string category, string scope, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(title, text, category, scope));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Create_TheLimitsOfTheTitleAndTheText_Are120And5000Characters()
    {
        ManagerSession manager = await RegisterAsync();

        (await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(new string('t', 120), new string('b', 5000)))).StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage longTitle = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(new string('t', 121)));
        HttpResponseMessage longText = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(text: new string('b', 5001)));

        (await longTitle.CodeAsync()).Should().Be("announcement.title.invalid");
        (await longText.CodeAsync()).Should().Be("announcement.body.invalid");
    }

    // ---- E-mail ao publicar ----

    [Fact]
    public async Task Create_WithoutAskingForEmail_SendsNothing_AndTheResponseSaysSo()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        (_, string ana) = await ResidentAccountAsync(manager, a101);

        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Sem e-mail")));

        created.GetProperty("notifiedByEmail").GetBoolean().Should().BeFalse();
        AnnouncementMails(ana, "Sem e-mail").Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmail_WritesToEveryoneWhoLivesInTheScope_WithTheMuralLink()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        (_, string ana) = await ResidentAccountAsync(manager, a101, fullName: "Ana Souza");
        (_, string bia) = await ResidentAccountAsync(manager, b201, fullName: "Bia Lima");
        await MakeManagerLiveInAsync(manager, a101);

        JsonElement created = await ReadCreatedAsync(
            await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Água cortada", "Volta às 18h.", "important", notifyByEmail: true)));

        created.GetProperty("notifiedByEmail").GetBoolean().Should().BeTrue();
        EmailMessage forAna = AnnouncementMails(ana, "Água cortada").Should().ContainSingle().Subject;
        forAna.Subject.Should().Be("Importante: Água cortada");
        forAna.ReplyTo.Should().Be(manager.User.Email.Value);
        forAna.TextBody.Split('\n').Should().Contain("https://app.test/mural");
        AnnouncementMails(bia, "Água cortada").Should().ContainSingle();
        AnnouncementMails(manager.User.Email.Value, "Água cortada").Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmail_ForABlock_OnlyWritesToWhoLivesInThatBlock()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        (_, string ana) = await ResidentAccountAsync(manager, a101);
        (_, string bia) = await ResidentAccountAsync(manager, b201);

        await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Só o bloco A", scope: "block", block: "Bloco A", notifyByEmail: true)));

        AnnouncementMails(ana, "Só o bloco A").Should().ContainSingle();
        AnnouncementMails(bia, "Só o bloco A").Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AskingForEmail_WithInvalidData_SendsNothing()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        (_, string ana) = await ResidentAccountAsync(manager, a101);
        int before = Emails.SentTo(ana).Length;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("", notifyByEmail: true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Emails.SentTo(ana).Should().HaveCount(before);
    }

    [Fact]
    public async Task Update_NeverWritesAgain_AndKeepsTheFlag()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        (_, string ana) = await ResidentAccountAsync(manager, a101);
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Original", notifyByEmail: true)));
        Guid id = created.GetProperty("id").GetGuid();

        JsonElement updated = await ReadOkAsync(
            await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Corrigido", notifyByEmail: true, version: created.GetProperty("version").GetString())));

        updated.GetProperty("notifiedByEmail").GetBoolean().Should().BeTrue();
        AnnouncementMails(ana, "Original").Should().ContainSingle();
        AnnouncementMails(ana, "Corrigido").Should().BeEmpty();
        Item(await ListAsync(manager), id).GetProperty("notifiedByEmail").GetBoolean().Should().BeTrue();
    }

    // ---- Lista do síndico ----

    [Fact]
    public async Task List_ReturnsTheNewestFirst_OnePageAtATime_NeverCached()
    {
        ManagerSession manager = await RegisterAsync();
        for (int i = 0; i < 5; i++)
        {
            await CreateAsync(manager, $"Aviso {i}");
        }

        HttpResponseMessage first = await _client.SendJsonAsync(HttpMethod.Get, $"{Route}?page=1&pageSize=2", manager.AccessToken);
        JsonElement second = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Get, $"{Route}?page=2&pageSize=2", manager.AccessToken));
        JsonElement last = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Get, $"{Route}?page=3&pageSize=2", manager.AccessToken));

        first.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement firstBody = await ReadOkAsync(first);
        Titles(firstBody).Should().Equal("Aviso 4", "Aviso 3");
        Titles(second).Should().Equal("Aviso 2", "Aviso 1");
        Titles(last).Should().Equal("Aviso 0");
        firstBody.GetProperty("total").GetInt32().Should().Be(5);
        firstBody.GetProperty("page").GetInt32().Should().Be(1);
        firstBody.GetProperty("pageSize").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_AnInvalidPage_Is400(string query)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Route}?{query}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("announcement.page.invalid");
    }

    [Fact]
    public async Task List_ShowsHowManyReadOutOfWhoLivesInTheScope()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        await ResidentTokenAsync(manager, a101, "dependent", "Bia Souza");
        string caio = await ResidentTokenAsync(manager, b201);
        Guid everyone = await CreateAsync(manager, "Para todos");
        Guid blockA = await CreateAsync(manager, "Bloco A", scope: "block", block: "Bloco A");
        await MarkReadAsync(ana, everyone);
        await MarkReadAsync(caio, everyone);
        await MarkReadAsync(ana, blockA);

        JsonElement list = await ListAsync(manager);

        JsonElement all = Item(list, everyone);
        all.GetProperty("recipients").GetInt32().Should().Be(3);
        all.GetProperty("readers").GetInt32().Should().Be(2);
        JsonElement block = Item(list, blockA);
        block.GetProperty("recipients").GetInt32().Should().Be(2);
        block.GetProperty("readers").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task List_TheManagerWhoLivesThereIsNotCountedAsARecipient()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await ResidentTokenAsync(manager, a101);
        await MakeManagerLiveInAsync(manager, a101);
        Guid id = await CreateAsync(manager);

        JsonElement item = Item(await ListAsync(manager), id);

        item.GetProperty("recipients").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task List_NeverShowsTheAnnouncementsOfAnotherCondominium()
    {
        ManagerSession ours = await RegisterAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        await CreateAsync(ours, "Nosso");
        await CreateAsync(theirs, "Deles");

        Titles(await ListAsync(ours)).Should().Equal("Nosso");
        Titles(await ListAsync(theirs)).Should().Equal("Deles");
    }

    // ---- Editar ----

    [Fact]
    public async Task Update_ReplacesTheAnnouncement_AndReturnsItWithANewVersion()
    {
        (ManagerSession manager, _) = await ManagerWithAUnitAsync();
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Antigo")));
        Guid id = created.GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Novo título", "Novo texto.", "important", "block", "Bloco A", version: created.GetProperty("version").GetString()));

        JsonElement updated = await ReadOkAsync(response);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        updated.GetProperty("id").GetGuid().Should().Be(id);
        updated.GetProperty("title").GetString().Should().Be("Novo título");
        updated.GetProperty("category").GetString().Should().Be("important");
        updated.GetProperty("scope").GetString().Should().Be("block");
        updated.GetProperty("block").GetString().Should().Be("Bloco A");
        updated.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.String);
        updated.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(created.GetProperty("createdAt").GetDateTimeOffset(), TimeSpan.FromMilliseconds(1));
        updated.GetProperty("version").GetString().Should().NotBe(created.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Update_KeepsWhoAlreadyRead()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Antigo")));
        Guid id = created.GetProperty("id").GetGuid();
        await MarkReadAsync(ana, id);
        string version = Item(await ListAsync(manager), id).GetProperty("version").GetString()!;

        JsonElement updated = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Corrigido", version: version)));

        updated.GetProperty("readers").GetInt32().Should().Be(1);
        updated.GetProperty("recipients").GetInt32().Should().Be(1);
        (await MineAsync(ana)).GetProperty("items")[0].GetProperty("read").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Update_TheNewScopeDecidesWhoSeesIt()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        string caio = await ResidentTokenAsync(manager, b201);
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Aviso")));
        Guid id = created.GetProperty("id").GetGuid();

        await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Aviso", scope: "block", block: "Bloco B", version: created.GetProperty("version").GetString())));

        Titles(await MineAsync(ana)).Should().BeEmpty();
        Titles(await MineAsync(caio)).Should().Equal("Aviso");
    }

    [Fact]
    public async Task Update_WithoutTheVersion_Is400_AndAStaleVersionIs409()
    {
        (ManagerSession manager, _) = await ManagerWithAUnitAsync();
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Original")));
        Guid id = created.GetProperty("id").GetGuid();
        string version = created.GetProperty("version").GetString()!;
        await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Da outra pessoa", version: version)));

        HttpResponseMessage without = await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Sem versão"));
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Minha", version: version));

        without.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await without.CodeAsync()).Should().Be("operations.version.required");
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        Titles(await ListAsync(manager)).Should().Equal("Da outra pessoa");
    }

    [Fact]
    public async Task Update_AnUnknownAnnouncement_OrOneOfAnotherCondominium_Is404()
    {
        ManagerSession ours = await RegisterAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid theirsId = await CreateAsync(theirs, "Deles");

        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(Guid.NewGuid()), ours.AccessToken, Body(version: "1"));
        HttpResponseMessage foreign = await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(theirsId), ours.AccessToken, Body("Invadido", version: "1"));

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.CodeAsync()).Should().Be("announcement.not_found");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Titles(await ListAsync(theirs)).Should().Equal("Deles");
    }

    [Fact]
    public async Task Update_InvalidData_Is400_AndChangesNothing()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Original")));
        Guid id = created.GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("", version: created.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("announcement.title.invalid");
        Titles(await ListAsync(manager)).Should().Equal("Original");
    }

    [Fact]
    public async Task Update_ToABlockWithNoUnits_Is422()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Original")));

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put, ItemRoute(created.GetProperty("id").GetGuid()), manager.AccessToken, Body("Original", scope: "block", block: "Bloco Z", version: created.GetProperty("version").GetString()));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("announcement.block.not_found");
    }

    // ---- Apagar ----

    [Fact]
    public async Task Delete_RemovesTheAnnouncementAndItsReads_With204()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        Guid doomed = await CreateAsync(manager, "Apagar");
        await CreateAsync(manager, "Ficar");
        await MarkReadAsync(ana, doomed);
        string version = Item(await ListAsync(manager), doomed).GetProperty("version").GetString()!;

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Delete, $"{ItemRoute(doomed)}?version={version}", manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Titles(await ListAsync(manager)).Should().Equal("Ficar");
        Titles(await MineAsync(ana)).Should().Equal("Ficar");
        (await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(doomed), ana)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutTheVersion_Is400_AStaleVersionIs409_AndTheAnnouncementStays()
    {
        ManagerSession manager = await RegisterAsync();
        JsonElement created = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body("Original")));
        Guid id = created.GetProperty("id").GetGuid();
        string version = created.GetProperty("version").GetString()!;
        await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Put, ItemRoute(id), manager.AccessToken, Body("Editado", version: version)));

        HttpResponseMessage without = await _client.SendJsonAsync(HttpMethod.Delete, ItemRoute(id), manager.AccessToken);
        HttpResponseMessage stale = await _client.SendJsonAsync(HttpMethod.Delete, $"{ItemRoute(id)}?version={version}", manager.AccessToken);

        without.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await without.CodeAsync()).Should().Be("operations.version.required");
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.CodeAsync()).Should().Be("concurrency.conflict");
        Titles(await ListAsync(manager)).Should().Equal("Editado");
    }

    [Fact]
    public async Task Delete_AnUnknownAnnouncement_OrOneAlreadyDeleted_OrOfAnotherCondominium_Is404()
    {
        ManagerSession ours = await RegisterAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        Guid theirsId = await CreateAsync(theirs, "Deles");
        JsonElement mine = await ReadCreatedAsync(await _client.SendJsonAsync(HttpMethod.Post, Route, ours.AccessToken, Body("Meu")));
        string route = $"{ItemRoute(mine.GetProperty("id").GetGuid())}?version={mine.GetProperty("version").GetString()}";
        (await _client.SendJsonAsync(HttpMethod.Delete, route, ours.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Delete, route, ours.AccessToken);
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Delete, $"{ItemRoute(Guid.NewGuid())}?version=1", ours.AccessToken);
        HttpResponseMessage foreign = await _client.SendJsonAsync(HttpMethod.Delete, $"{ItemRoute(theirsId)}?version=1", ours.AccessToken);

        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Titles(await ListAsync(theirs)).Should().Equal("Deles");
    }

    // ---- O mural de quem mora ----

    [Fact]
    public async Task Mine_ShowsTheWholeCondominium_TheBlockAndTheUnitWhereTheResidentLives_AndNothingElse()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid a102 = await CreateUnitAsync(manager, "Bloco A", "102");
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Todos");
        await CreateAsync(manager, "Meu bloco", scope: "block", block: "Bloco A");
        await CreateAsync(manager, "Minha unidade", scope: "unit", unitId: a101);
        await CreateAsync(manager, "Outro bloco", scope: "block", block: "Bloco B");
        await CreateAsync(manager, "Outra unidade", scope: "unit", unitId: a102);
        await CreateAsync(manager, "Unidade do outro bloco", scope: "unit", unitId: b201);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, MineRoute, ana);

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement mural = await ReadOkAsync(response);
        Titles(mural).Should().Equal("Minha unidade", "Meu bloco", "Todos");
        mural.GetProperty("total").GetInt32().Should().Be(3);
        mural.GetProperty("unread").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Mine_EachAnnouncementTellsItsBlockOrItsUnit_AndTheWholeCondominiumTellsNeither()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Todos");
        await CreateAsync(manager, "Bloco", scope: "block", block: "Bloco A");
        await CreateAsync(manager, "Apartamento", scope: "unit", unitId: a101);

        JsonElement mural = await MineAsync(ana);

        JsonElement unit = Item(mural, "Apartamento");
        unit.GetProperty("scope").GetString().Should().Be("unit");
        unit.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
        unit.GetProperty("unit").GetProperty("id").GetGuid().Should().Be(a101);
        unit.GetProperty("unit").GetProperty("block").GetString().Should().Be("Bloco A");
        unit.GetProperty("unit").GetProperty("number").GetString().Should().Be("101");
        JsonElement block = Item(mural, "Bloco");
        block.GetProperty("block").GetString().Should().Be("Bloco A");
        block.GetProperty("unit").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement everyone = Item(mural, "Todos");
        everyone.GetProperty("block").ValueKind.Should().Be(JsonValueKind.Null);
        everyone.GetProperty("unit").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mine_Category_FiltersTheListAndItsTotal_ButNotTheUnreadCounters()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Geral", category: "general");
        await CreateAsync(manager, "Corte de água", category: "maintenance");
        await CreateAsync(manager, "Elevador", category: "maintenance");
        await CreateAsync(manager, "Assembleia", category: "important");

        JsonElement mural = await MineAsync(ana, "category=maintenance");

        Titles(mural).Should().Equal("Elevador", "Corte de água");
        mural.GetProperty("total").GetInt32().Should().Be(2);
        mural.GetProperty("unread").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Mine_UnreadByCategory_CountsTheWholeBoard_AndFollowsWhatWasRead()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        Guid read = await CreateAsync(manager, "Geral lido", category: "general");
        await CreateAsync(manager, "Geral", category: "general");
        await CreateAsync(manager, "Manutenção 1", category: "maintenance");
        await CreateAsync(manager, "Manutenção 2", category: "maintenance");
        await CreateAsync(manager, "Importante", category: "important");
        await CreateAsync(manager, "Do outro bloco", category: "important", scope: "block", block: "Bloco B");
        await MarkReadAsync(ana, read);

        JsonElement mural = await MineAsync(ana, "unreadOnly=true&category=general&pageSize=1");

        JsonElement counts = mural.GetProperty("unreadByCategory");
        counts.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("general", "maintenance", "important");
        counts.GetProperty("general").GetInt32().Should().Be(1);
        counts.GetProperty("maintenance").GetInt32().Should().Be(2);
        counts.GetProperty("important").GetInt32().Should().Be(1);
        mural.GetProperty("unread").GetInt32().Should().Be(4);
    }

    [Theory]
    [InlineData("category=urgente")]
    [InlineData("category=1")]
    public async Task Mine_ACategoryThatDoesNotExist_Is400(string query)
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{MineRoute}?{query}", ana);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("announcement.category.invalid");
    }

    [Fact]
    public async Task Mine_ABlankCategory_ShowsEveryCategory()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Geral", category: "general");
        await CreateAsync(manager, "Importante", category: "important");

        Titles(await MineAsync(ana, "category=")).Should().Equal("Importante", "Geral");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("tenant")]
    [InlineData("dependent")]
    public async Task Mine_WhoeverLivesInTheUnitSeesItsAnnouncements_InAnyRole(string kind)
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string resident = await ResidentTokenAsync(manager, a101, kind);
        await CreateAsync(manager, "Da unidade", scope: "unit", unitId: a101);

        Titles(await MineAsync(resident)).Should().Equal("Da unidade");
    }

    [Fact]
    public async Task Mine_TheMuralDoesNotRevealTheAuthorNorWhoRead()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager);

        JsonElement item = (await MineAsync(ana)).GetProperty("items")[0];

        item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "title", "body", "category", "scope", "block", "unit", "createdAt", "updatedAt", "read", "readAt");
    }

    [Fact]
    public async Task Mine_SomeoneWhoLivesNowhere_SeesAnEmptyBoard()
    {
        (ManagerSession manager, _) = await ManagerWithAUnitAsync();
        await CreateAsync(manager, "Para todos");

        JsonElement mural = await MineAsync(manager.AccessToken);

        mural.GetProperty("items").GetArrayLength().Should().Be(0);
        mural.GetProperty("total").GetInt32().Should().Be(0);
        mural.GetProperty("unread").GetInt32().Should().Be(0);
        mural.GetProperty("unreadByCategory").GetProperty("general").GetInt32().Should().Be(0);
        mural.GetProperty("unreadByCategory").GetProperty("maintenance").GetInt32().Should().Be(0);
        mural.GetProperty("unreadByCategory").GetProperty("important").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Mine_TheManagerWhoLivesThere_DoesNotSeeHisOwnAnnouncements()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await MakeManagerLiveInAsync(manager, a101);
        await CreateAsync(manager, "Meu aviso");

        Titles(await MineAsync(manager.AccessToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Mine_AResident_DoesNotSeeTheAnnouncementsOfABlockWhereTheyDoNotLive()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Do bloco A", scope: "block", block: "Bloco A");
        await CreateAsync(manager, "Do bloco B", scope: "block", block: "Bloco B");

        Titles(await MineAsync(ana)).Should().Equal("Do bloco A");
    }

    [Fact]
    public async Task Mine_TheUnreadCounterCoversTheWholeBoard_AndUnreadOnlyFiltersTheList()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        Guid read = await CreateAsync(manager, "Lido");
        await CreateAsync(manager, "Não lido 1");
        await CreateAsync(manager, "Não lido 2");
        await MarkReadAsync(ana, read);

        JsonElement all = await MineAsync(ana, "pageSize=1");
        JsonElement unread = await MineAsync(ana, "unreadOnly=true");

        all.GetProperty("items").GetArrayLength().Should().Be(1);
        all.GetProperty("total").GetInt32().Should().Be(3);
        all.GetProperty("unread").GetInt32().Should().Be(2);
        Titles(unread).Should().Equal("Não lido 2", "Não lido 1");
        unread.GetProperty("total").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Mine_IsPaged()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        for (int i = 0; i < 5; i++)
        {
            await CreateAsync(manager, $"Aviso {i}");
        }

        JsonElement second = await MineAsync(ana, "page=2&pageSize=2");

        Titles(second).Should().Equal("Aviso 2", "Aviso 1");
        second.GetProperty("total").GetInt32().Should().Be(5);
        second.GetProperty("page").GetInt32().Should().Be(2);
        second.GetProperty("pageSize").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Mine_AnInvalidPage_Is400(string query)
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{MineRoute}?{query}", ana);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("announcement.page.invalid");
    }

    [Fact]
    public async Task Mine_NeverShowsTheAnnouncementsOfAnotherCondominium()
    {
        (ManagerSession ours, Guid a101) = await ManagerWithAUnitAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        string ana = await ResidentTokenAsync(ours, a101);
        await CreateAsync(ours, "Nosso");
        await CreateAsync(theirs, "Deles");

        Titles(await MineAsync(ana)).Should().Equal("Nosso");
    }

    // ---- Marcar como lido ----

    [Fact]
    public async Task MarkRead_Returns204_AndTheMuralShowsItReadWithTheMoment()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        Guid id = await CreateAsync(manager, "Aviso");

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), ana);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement mural = await MineAsync(ana);
        JsonElement item = mural.GetProperty("items")[0];
        item.GetProperty("read").GetBoolean().Should().BeTrue();
        item.GetProperty("readAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
        mural.GetProperty("unread").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkRead_Twice_Is204_AndKeepsTheFirstMoment()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        Guid id = await CreateAsync(manager);
        await MarkReadAsync(ana, id);
        DateTimeOffset first = (await MineAsync(ana)).GetProperty("items")[0].GetProperty("readAt").GetDateTimeOffset();

        HttpResponseMessage again = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), ana);

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MineAsync(ana)).GetProperty("items")[0].GetProperty("readAt").GetDateTimeOffset().Should().Be(first);
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementOfAnotherBlockOrUnit_Is404_AsIfItDidNotExist()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        Guid otherBlock = await CreateAsync(manager, "Bloco B", scope: "block", block: "Bloco B");
        Guid otherUnit = await CreateAsync(manager, "Unidade 201", scope: "unit", unitId: b201);

        HttpResponseMessage block = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(otherBlock), ana);
        HttpResponseMessage unit = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(otherUnit), ana);
        HttpResponseMessage unknown = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(Guid.NewGuid()), ana);

        block.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await block.CodeAsync()).Should().Be("announcement.not_found");
        unit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Item(await ListAsync(manager), otherBlock).GetProperty("readers").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkRead_AnAnnouncementOfAnotherCondominium_Is404()
    {
        (ManagerSession ours, Guid a101) = await ManagerWithAUnitAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        string ana = await ResidentTokenAsync(ours, a101);
        Guid theirsId = await CreateAsync(theirs, "Deles");

        (await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(theirsId), ana)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_ByTheAuthorOrByWhoLivesNowhere_Is404()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid id = await CreateAsync(manager);

        (await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), manager.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await MakeManagerLiveInAsync(manager, a101);

        (await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), manager.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        Item(await ListAsync(manager), id).GetProperty("readers").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkRead_ManyAtTheSameTime_AreAllAccepted_AndCountOnce()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        Guid id = await CreateAsync(manager);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), ana)));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NoContent);
        Item(await ListAsync(manager), id).GetProperty("readers").GetInt32().Should().Be(1);
    }

    // ---- Marcar todos como lidos ----

    [Fact]
    public async Task MarkAllRead_Returns200WithHowManyWereMarked_AndTheMuralShowsEverythingRead()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await CreateUnitAsync(manager, "Bloco B", "201");
        string ana = await ResidentTokenAsync(manager, a101);
        Guid already = await CreateAsync(manager, "Já lido");
        await CreateAsync(manager, "Todos");
        await CreateAsync(manager, "Meu bloco", scope: "block", block: "Bloco A");
        await CreateAsync(manager, "Outro bloco", scope: "block", block: "Bloco B");
        await MarkReadAsync(ana, already);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, ana);

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ReadOkAsync(response)).GetProperty("marked").GetInt32().Should().Be(2);
        JsonElement mural = await MineAsync(ana);
        mural.GetProperty("unread").GetInt32().Should().Be(0);
        mural.GetProperty("items").EnumerateArray().Should().OnlyContain(item => item.GetProperty("read").GetBoolean());
        Item(await ListAsync(manager), "Outro bloco").GetProperty("readers").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_Twice_Is200_AndTheSecondMarksNothing()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager);
        await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, ana);
        DateTimeOffset first = (await MineAsync(ana)).GetProperty("items")[0].GetProperty("readAt").GetDateTimeOffset();

        JsonElement again = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, ana));

        again.GetProperty("marked").GetInt32().Should().Be(0);
        (await MineAsync(ana)).GetProperty("items")[0].GetProperty("readAt").GetDateTimeOffset().Should().Be(first);
    }

    [Fact]
    public async Task MarkAllRead_WithACategory_MarksOnlyThatCategory()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Geral", category: "general");
        await CreateAsync(manager, "Corte de água", category: "maintenance");
        await CreateAsync(manager, "Assembleia", category: "important");

        JsonElement result = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Post, $"{ReadAllRoute}?category=maintenance", ana));

        result.GetProperty("marked").GetInt32().Should().Be(1);
        JsonElement counts = (await MineAsync(ana)).GetProperty("unreadByCategory");
        counts.GetProperty("general").GetInt32().Should().Be(1);
        counts.GetProperty("maintenance").GetInt32().Should().Be(0);
        counts.GetProperty("important").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MarkAllRead_ACategoryThatDoesNotExist_Is400_AndMarksNothing()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ReadAllRoute}?category=urgente", ana);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("announcement.category.invalid");
        (await MineAsync(ana)).GetProperty("unread").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MarkAllRead_ByWhoLivesNowhereOrByTheAuthor_MarksNothing()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        await CreateAsync(manager, "Para todos");

        JsonElement nowhere = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, manager.AccessToken));
        await MakeManagerLiveInAsync(manager, a101);
        JsonElement author = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, manager.AccessToken));

        nowhere.GetProperty("marked").GetInt32().Should().Be(0);
        author.GetProperty("marked").GetInt32().Should().Be(0);
        Item(await ListAsync(manager), "Para todos").GetProperty("readers").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_NeverTouchesTheAnnouncementsOfAnotherCondominium()
    {
        (ManagerSession ours, Guid a101) = await ManagerWithAUnitAsync();
        ManagerSession theirs = await ManagerSetup.RegisterAsync(fixture, _client);
        string ana = await ResidentTokenAsync(ours, a101);
        await CreateAsync(ours, "Nosso");
        await CreateAsync(theirs, "Deles");

        JsonElement result = await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, ana));

        result.GetProperty("marked").GetInt32().Should().Be(1);
        Item(await ListAsync(theirs), "Deles").GetProperty("readers").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_ManyAtTheSameTime_AreAllAccepted_AndEachAnnouncementCountsOnce()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        string ana = await ResidentTokenAsync(manager, a101);
        await CreateAsync(manager, "Um");
        await CreateAsync(manager, "Dois");
        await CreateAsync(manager, "Três");

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.SendJsonAsync(HttpMethod.Post, ReadAllRoute, ana)));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        int marked = 0;
        foreach (HttpResponseMessage response in responses)
        {
            marked += (await ReadOkAsync(response)).GetProperty("marked").GetInt32();
        }

        marked.Should().Be(3);
        (await MineAsync(ana)).GetProperty("unread").GetInt32().Should().Be(0);
        JsonElement list = await ListAsync(manager);
        list.GetProperty("items").EnumerateArray().Should().OnlyContain(item => item.GetProperty("readers").GetInt32() == 1);
    }

    // ---- Apoio ----

    private static string[] Titles(JsonElement page) => [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("title").GetString()!)];

    private static JsonElement Item(JsonElement page, Guid id) => page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);

    private static JsonElement Item(JsonElement page, string title) => page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("title").GetString() == title);

    private static JsonObject Body(
        string title = "Aviso do síndico",
        string text = "Texto do aviso.",
        string category = "general",
        string scope = "condominium",
        string? block = null,
        Guid? unitId = null,
        string? version = null,
        bool? notifyByEmail = null) => new()
        {
            ["version"] = version,
            ["title"] = title,
            ["body"] = text,
            ["category"] = category,
            ["scope"] = scope,
            ["block"] = block,
            ["unitId"] = unitId,
            ["notifyByEmail"] = notifyByEmail,
        };

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateOperationsAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    /// <summary>O síndico com a unidade 101 do Bloco A cadastrada.</summary>
    private async Task<(ManagerSession Manager, Guid UnitId)> ManagerWithAUnitAsync()
    {
        ManagerSession manager = await RegisterAsync();

        return (manager, await CreateUnitAsync(manager, "Bloco A", "101"));
    }

    private async Task<Guid> CreateUnitAsync(ManagerSession manager, string block, string number)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, "/api/units", manager.AccessToken, new JsonObject { ["block"] = block, ["number"] = number, ["type"] = "apartment" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private Task<string> ResidentTokenAsync(ManagerSession manager, Guid unitId, string kind = "owner", string fullName = "Maria Souza") =>
        ResidentSetup.TokenAsync(fixture, _client, manager, unitId, kind, fullName);

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    /// <summary>Os e-mails de comunicado que chegaram ao endereço, achados pelo título (o convite e os outros avisos da conta ficam de fora).</summary>
    private EmailMessage[] AnnouncementMails(string address, string title) =>
        [.. Emails.SentTo(address).Where(message => message.Subject.EndsWith(title, StringComparison.Ordinal))];

    private async Task<(string Token, string Email)> ResidentAccountAsync(ManagerSession manager, Guid unitId, string kind = "owner", string fullName = "Maria Souza") =>
        await ResidentSetup.AccountAsync(fixture, _client, manager, unitId, kind, fullName);

    private async Task<Guid> CreateAsync(
        ManagerSession manager, string title = "Aviso do síndico", string scope = "condominium", string? block = null, Guid? unitId = null, string category = "general")
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(title, category: category, scope: scope, block: block, unitId: unitId));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task MarkReadAsync(string token, Guid id)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, ReadRoute(id), token);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<JsonElement> ListAsync(ManagerSession manager) => await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Get, Route, manager.AccessToken));

    private async Task<JsonElement> MineAsync(string token, string? query = null) =>
        await ReadOkAsync(await _client.SendJsonAsync(HttpMethod.Get, query is null ? MineRoute : $"{MineRoute}?{query}", token));

    private static async Task<JsonElement> ReadOkAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<JsonElement> ReadCreatedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>O vínculo do síndico é gravado direto no banco para o teste não depender de outra rota; o caminho pela API tem os testes de `ResidentSelfEndpointsTests`.</summary>
    private async Task MakeManagerLiveInAsync(ManagerSession manager, Guid unitId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        CondominiumDbContext db = scope.ServiceProvider.GetRequiredService<CondominiumDbContext>();
        Person person = await db.People.IgnoreQueryFilters().SingleAsync(p => p.UserId == manager.User.Id, Ct);
        Occupancy occupancy = Occupancy.Start(manager.CondominiumId, person.Id, unitId, OccupancyKind.Owner, new DateOnly(2024, 3, 1), new DateOnly(2026, 11, 7), DateTimeOffset.UtcNow).Value;
        db.Occupancies.Add(occupancy);
        await db.SaveChangesAsync(Ct);
    }
}
