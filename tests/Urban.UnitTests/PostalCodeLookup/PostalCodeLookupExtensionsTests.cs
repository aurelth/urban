using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Time;

namespace Urban.UnitTests.PostalCodeLookup;

public class PostalCodeLookupExtensionsTests
{
    private const string BrasilApiOk = """{"state":"SP","city":"São Paulo","neighborhood":"Bela Vista","street":"Avenida Paulista"}""";

    private const string ViaCepOk = """{"logradouro":"Avenida Paulista","bairro":"Bela Vista","localidade":"São Paulo","uf":"SP"}""";

    [Fact]
    public void AddPostalCodeLookup_ReadsTheProviderUrlsFromTheConfiguration()
    {
        using ServiceProvider provider = Build(Settings());

        PostalCodeLookupOptions options = provider.GetRequiredService<PostalCodeLookupOptions>();

        options.BrasilApi.BaseUrl.Should().Be("https://brasilapi.test/");
        options.ViaCep.BaseUrl.Should().Be("https://viacep.test/");
    }

    [Fact]
    public void AddPostalCodeLookup_RegistersTheCachedLookup()
    {
        using ServiceProvider provider = Build(Settings());

        provider.GetRequiredService<IPostalCodeLookup>().Should().BeOfType<CachedPostalCodeLookup>();
    }

    [Theory]
    [InlineData(nameof(BrasilApiPostalCodeLookup), "https://brasilapi.test/")]
    [InlineData(nameof(ViaCepPostalCodeLookup), "https://viacep.test/")]
    public void AddPostalCodeLookup_GivesEachProviderABoundedClient(string name, string baseUrl)
    {
        using ServiceProvider provider = Build(Settings());

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);

        client.BaseAddress.Should().Be(baseUrl);
        client.Timeout.Should().Be(TimeSpan.FromSeconds(3));
        client.MaxResponseContentBufferSize.Should().Be(64 * 1024);
    }

    [Fact]
    public async Task AddPostalCodeLookup_AsksTheBrasilApiFirst()
    {
        StubHttpHandler brasilApi = StubHttpHandler.Returning(HttpStatusCode.OK, BrasilApiOk);
        StubHttpHandler viaCep = StubHttpHandler.Returning(HttpStatusCode.OK, ViaCepOk);
        using ServiceProvider provider = Build(Settings(), brasilApi, viaCep);

        PostalCodeLookupResult result = await provider.GetRequiredService<IPostalCodeLookup>().LookupAsync("01310100", default);

        result.Status.Should().Be(PostalCodeLookupStatus.Found);
        brasilApi.Requests.Should().ContainSingle();
        viaCep.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AddPostalCodeLookup_FallsBackToViaCepWhenTheBrasilApiIsDown()
    {
        StubHttpHandler brasilApi = StubHttpHandler.Returning(HttpStatusCode.ServiceUnavailable, string.Empty);
        StubHttpHandler viaCep = StubHttpHandler.Returning(HttpStatusCode.OK, ViaCepOk);
        using ServiceProvider provider = Build(Settings(), brasilApi, viaCep);

        PostalCodeLookupResult result = await provider.GetRequiredService<IPostalCodeLookup>().LookupAsync("01310100", default);

        result.Address.Should().Be(ScriptedLookup.Paulista);
        viaCep.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task AddPostalCodeLookup_SharesOneCacheAcrossEveryResolvedLookup()
    {
        StubHttpHandler brasilApi = StubHttpHandler.Returning(HttpStatusCode.OK, BrasilApiOk);
        StubHttpHandler viaCep = StubHttpHandler.Returning(HttpStatusCode.OK, ViaCepOk);
        using ServiceProvider provider = Build(Settings(), brasilApi, viaCep);

        using (IServiceScope first = provider.CreateScope())
        {
            await first.ServiceProvider.GetRequiredService<IPostalCodeLookup>().LookupAsync("01310100", default);
        }

        using (IServiceScope second = provider.CreateScope())
        {
            await second.ServiceProvider.GetRequiredService<IPostalCodeLookup>().LookupAsync("01310100", default);
        }

        brasilApi.Requests.Should().ContainSingle();
    }

    [Fact]
    public void AddPostalCodeLookup_RefusesAMissingSection()
    {
        Action build = () => Build([]);

        build.Should().Throw<InvalidOperationException>().WithMessage("*PostalCodeLookup:BrasilApi:BaseUrl*");
    }

    [Fact]
    public void AddPostalCodeLookup_RefusesAnInvalidViaCepUrl()
    {
        Dictionary<string, string?> settings = Settings();
        settings["PostalCodeLookup:ViaCep:BaseUrl"] = "http://viacep.test/";

        Action build = () => Build(settings);

        build.Should().Throw<InvalidOperationException>().WithMessage("*PostalCodeLookup:ViaCep:BaseUrl*");
    }

    private static Dictionary<string, string?> Settings() => new()
    {
        ["PostalCodeLookup:BrasilApi:BaseUrl"] = "https://brasilapi.test/",
        ["PostalCodeLookup:ViaCep:BaseUrl"] = "https://viacep.test/",
    };

    private static ServiceProvider Build(Dictionary<string, string?> settings, StubHttpHandler? brasilApi = null, StubHttpHandler? viaCep = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero)));
        services.AddPostalCodeLookup(configuration);
        if (brasilApi is not null)
        {
            services.AddHttpClient<BrasilApiPostalCodeLookup>().ConfigurePrimaryHttpMessageHandler(() => brasilApi);
        }

        if (viaCep is not null)
        {
            services.AddHttpClient<ViaCepPostalCodeLookup>().ConfigurePrimaryHttpMessageHandler(() => viaCep);
        }

        return services.BuildServiceProvider();
    }
}
