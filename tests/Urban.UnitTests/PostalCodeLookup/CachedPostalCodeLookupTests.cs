using FluentAssertions;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Time;

namespace Urban.UnitTests.PostalCodeLookup;

public class CachedPostalCodeLookupTests
{
    private const string Cep = "01310100";

    private static readonly PostalCodeLookupResult Found = PostalCodeLookupResult.Found(ScriptedLookup.Paulista);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task LookupAsync_TheSecondQueryForTheSameCepDoesNotCallTheProviders()
    {
        ScriptedLookup inner = new(Found);
        CachedPostalCodeLookup lookup = Create(inner);

        PostalCodeLookupResult first = await lookup.LookupAsync(Cep, default);
        PostalCodeLookupResult second = await lookup.LookupAsync(Cep, default);

        first.Should().Be(Found);
        second.Should().Be(Found);
        inner.Requested.Should().Equal(Cep);
    }

    [Fact]
    public async Task LookupAsync_KeepsEachCepOnItsOwn()
    {
        ScriptedLookup inner = new(Found, PostalCodeLookupResult.NotFound);
        CachedPostalCodeLookup lookup = Create(inner);

        PostalCodeLookupResult first = await lookup.LookupAsync(Cep, default);
        PostalCodeLookupResult other = await lookup.LookupAsync("20040020", default);

        first.Should().Be(Found);
        other.Should().Be(PostalCodeLookupResult.NotFound);
        inner.Requested.Should().Equal(Cep, "20040020");
    }

    [Fact]
    public async Task LookupAsync_KeepsAFoundCepForSevenDays()
    {
        ScriptedLookup inner = new(Found);
        CachedPostalCodeLookup lookup = Create(inner);
        await lookup.LookupAsync(Cep, default);

        _clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1));
        await lookup.LookupAsync(Cep, default);
        inner.Requested.Should().HaveCount(1);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await lookup.LookupAsync(Cep, default);
        inner.Requested.Should().HaveCount(2);
    }

    [Fact]
    public async Task LookupAsync_KeepsANotFoundCepForOnlyOneHour()
    {
        ScriptedLookup inner = new(PostalCodeLookupResult.NotFound, Found);
        CachedPostalCodeLookup lookup = Create(inner);
        (await lookup.LookupAsync(Cep, default)).Should().Be(PostalCodeLookupResult.NotFound);

        _clock.Advance(TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1));
        (await lookup.LookupAsync(Cep, default)).Should().Be(PostalCodeLookupResult.NotFound);
        inner.Requested.Should().HaveCount(1);

        _clock.Advance(TimeSpan.FromSeconds(1));
        (await lookup.LookupAsync(Cep, default)).Should().Be(Found);
        inner.Requested.Should().HaveCount(2);
    }

    [Fact]
    public async Task LookupAsync_NeverKeepsAnUnavailableAnswer()
    {
        ScriptedLookup inner = new(PostalCodeLookupResult.Unavailable, Found);
        CachedPostalCodeLookup lookup = Create(inner);

        PostalCodeLookupResult first = await lookup.LookupAsync(Cep, default);
        PostalCodeLookupResult second = await lookup.LookupAsync(Cep, default);

        first.Should().Be(PostalCodeLookupResult.Unavailable);
        second.Should().Be(Found);
        inner.Requested.Should().HaveCount(2);
    }

    [Fact]
    public async Task LookupAsync_SharesTheCacheBetweenInstancesThatUseTheSameStore()
    {
        PostalCodeCache cache = new(_clock);
        ScriptedLookup first = new(Found);
        ScriptedLookup second = new(PostalCodeLookupResult.Unavailable);
        await new CachedPostalCodeLookup(first, cache).LookupAsync(Cep, default);

        PostalCodeLookupResult result = await new CachedPostalCodeLookup(second, cache).LookupAsync(Cep, default);

        result.Should().Be(Found);
        second.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenTheStoreIsFull_StopsKeepingNewCepsButKeepsServingTheOnesItHas()
    {
        ScriptedLookup inner = new(Found);
        CachedPostalCodeLookup lookup = Create(inner, maxEntries: 2);
        await lookup.LookupAsync("11111111", default);
        await lookup.LookupAsync("22222222", default);
        await lookup.LookupAsync("33333333", default);
        await lookup.LookupAsync("33333333", default);
        await lookup.LookupAsync("11111111", default);

        inner.Requested.Should().Equal("11111111", "22222222", "33333333", "33333333");
    }

    [Fact]
    public async Task LookupAsync_WhenTheStoreIsFull_MakesRoomByDroppingExpiredEntries()
    {
        ScriptedLookup inner = new(Found);
        CachedPostalCodeLookup lookup = Create(inner, maxEntries: 2);
        await lookup.LookupAsync("11111111", default);
        await lookup.LookupAsync("22222222", default);
        _clock.Advance(TimeSpan.FromDays(8));

        await lookup.LookupAsync("33333333", default);
        await lookup.LookupAsync("33333333", default);

        inner.Requested.Should().Equal("11111111", "22222222", "33333333");
    }

    private CachedPostalCodeLookup Create(ScriptedLookup inner, int maxEntries = 1000) =>
        new(inner, new PostalCodeCache(_clock, maxEntries));
}
