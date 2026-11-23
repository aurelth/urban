using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Urban.Api.Hubs;
using Urban.Operations.Infrastructure;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// O aviso em tempo real do mural, de ponta a ponta: um cliente SignalR de verdade abre a conexão com o token na URL (como o navegador faz
/// com WebSocket), o síndico publica, edita e apaga pela API, e só quem mora no alcance é avisado.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AnnouncementHubTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Route = "/api/announcements";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    private readonly List<Listener> _listeners = [];

    public Task InitializeAsync() => fixture.Factory.Services.MigrateOperationsAsync();

    public async Task DisposeAsync()
    {
        foreach (Listener listener in _listeners)
        {
            await listener.DisposeAsync();
        }

        _client.Dispose();
    }

    [Fact]
    public async Task The_hub_refuses_a_connection_without_a_token()
    {
        await using Listener nobody = Connect(null);

        Func<Task> start = () => nobody.StartAsync();

        await start.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task The_hub_refuses_a_token_that_is_not_valid()
    {
        await using Listener forged = Connect("nao-e-um-token");

        Func<Task> start = () => forged.StartAsync();

        await start.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task A_resident_of_the_audience_is_told_when_the_manager_publishes()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Listener ana = await ListenAsync(await ResidentTokenAsync(manager, a101));

        Guid id = await PublishAsync(manager, "Corte de água");

        (await ana.NextAsync()).Should().Be(("published", id));
    }

    [Fact]
    public async Task Only_the_residents_of_the_audience_are_told()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        Listener ana = await ListenAsync(await ResidentTokenAsync(manager, a101));
        Listener caio = await ListenAsync(await ResidentTokenAsync(manager, b201, fullName: "Caio Lima"));

        Guid blockA = await PublishAsync(manager, "Só o bloco A", scope: "block", block: "Bloco A");
        Guid everyone = await PublishAsync(manager, "Para todos");

        (await ana.NextAsync()).Should().Be(("published", blockA));
        (await ana.NextAsync()).Should().Be(("published", everyone));
        (await caio.NextAsync()).Should().Be(("published", everyone));
    }

    [Fact]
    public async Task The_manager_who_wrote_is_not_told_about_their_own_announcement()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Listener ana = await ListenAsync(await ResidentTokenAsync(manager, a101));
        Listener writer = await ListenAsync(manager.AccessToken);

        Guid id = await PublishAsync(manager, "Meu aviso");

        (await ana.NextAsync()).Should().Be(("published", id));
        (await writer.NothingAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Editing_into_another_block_tells_the_old_and_the_new_audience()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Guid b201 = await CreateUnitAsync(manager, "Bloco B", "201");
        Guid c301 = await CreateUnitAsync(manager, "Bloco C", "301");
        Listener ana = await ListenAsync(await ResidentTokenAsync(manager, a101));
        Listener caio = await ListenAsync(await ResidentTokenAsync(manager, b201, fullName: "Caio Lima"));
        Listener davi = await ListenAsync(await ResidentTokenAsync(manager, c301, fullName: "Davi Rocha"));
        JsonElement created = await PublishFullAsync(manager, "Só o bloco A", scope: "block", block: "Bloco A");
        Guid id = created.GetProperty("id").GetGuid();
        (await ana.NextAsync()).Should().Be(("published", id));

        HttpResponseMessage edited = await _client.SendJsonAsync(
            HttpMethod.Put, $"{Route}/{id}", manager.AccessToken, Body("Agora o bloco B", "block", "Bloco B", created.GetProperty("version").GetString()));
        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync(Ct));

        (await ana.NextAsync()).Should().Be(("updated", id));
        (await caio.NextAsync()).Should().Be(("updated", id));
        Guid sentinel = await PublishAsync(manager, "Para todos");
        (await davi.NextAsync()).Should().Be(("published", sentinel));
    }

    [Fact]
    public async Task Deleting_tells_the_audience_that_it_was_removed()
    {
        (ManagerSession manager, Guid a101) = await ManagerWithAUnitAsync();
        Listener ana = await ListenAsync(await ResidentTokenAsync(manager, a101));
        JsonElement created = await PublishFullAsync(manager, "Apagar");
        Guid id = created.GetProperty("id").GetGuid();
        (await ana.NextAsync()).Should().Be(("published", id));

        HttpResponseMessage deleted = await _client.SendJsonAsync(HttpMethod.Delete, $"{Route}/{id}?version={created.GetProperty("version").GetString()}", manager.AccessToken);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync(Ct));

        (await ana.NextAsync()).Should().Be(("removed", id));
    }

    private Listener Connect(string? token)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Factory.Server.BaseAddress, AnnouncementsHub.Path),
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.AccessTokenProvider = () => Task.FromResult(token);
                    options.WebSocketFactory = async (context, cancellationToken) =>
                        await fixture.Factory.Server.CreateWebSocketClient().ConnectAsync(WithTokenInTheUrl(context.Uri, token), cancellationToken);
                })
            .Build();
        Listener listener = new(connection);
        _listeners.Add(listener);

        return listener;
    }

    /// <summary>O navegador não põe cabeçalho no WebSocket: o token de acesso vai na URL, em <c>access_token</c>. O teste faz o mesmo.</summary>
    private static Uri WithTokenInTheUrl(Uri uri, string? token) =>
        token is null ? uri : new UriBuilder(uri) { Query = $"access_token={Uri.EscapeDataString(token)}" }.Uri;

    private async Task<Listener> ListenAsync(string token)
    {
        Listener listener = Connect(token);
        await listener.StartAsync();

        return listener;
    }

    private async Task<(ManagerSession Manager, Guid UnitId)> ManagerWithAUnitAsync()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

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

    private async Task<Guid> PublishAsync(ManagerSession manager, string title, string scope = "condominium", string? block = null) =>
        (await PublishFullAsync(manager, title, scope, block)).GetProperty("id").GetGuid();

    private async Task<JsonElement> PublishFullAsync(ManagerSession manager, string title, string scope = "condominium", string? block = null)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route, manager.AccessToken, Body(title, scope, block));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static JsonObject Body(string title, string scope = "condominium", string? block = null, string? version = null) => new()
    {
        ["version"] = version,
        ["title"] = title,
        ["body"] = "Texto do aviso.",
        ["category"] = "general",
        ["scope"] = scope,
        ["block"] = block,
    };

    private sealed class Listener : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly Channel<(string Kind, Guid Id)> _received = Channel.CreateUnbounded<(string Kind, Guid Id)>();

        public Listener(HubConnection connection)
        {
            _connection = connection;
            _connection.On<JsonElement>(AnnouncementsHub.ChangedMethod, message =>
                _received.Writer.TryWrite((message.GetProperty("kind").GetString()!, message.GetProperty("id").GetGuid())));
        }

        public Task StartAsync() => _connection.StartAsync(Ct);

        public async Task<(string Kind, Guid Id)> NextAsync()
        {
            using CancellationTokenSource timeout = new(Patience);

            return await _received.Reader.ReadAsync(timeout.Token);
        }

        /// <returns>Se nada chegou nesse meio-tempo.</returns>
        public async Task<bool> NothingAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);

            return !_received.Reader.TryPeek(out _);
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
