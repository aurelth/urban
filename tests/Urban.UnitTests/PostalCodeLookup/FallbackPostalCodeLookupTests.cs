using FluentAssertions;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.PostalCodeLookup;

public class FallbackPostalCodeLookupTests
{
    private const string Cep = "01310100";

    private static readonly PostalCodeLookupResult Found = PostalCodeLookupResult.Found(ScriptedLookup.Paulista);

    private static readonly PostalCodeLookupResult FoundByTheBackup =
        PostalCodeLookupResult.Found(ScriptedLookup.Paulista with { Street = "Av. Paulista" });

    [Fact]
    public async Task LookupAsync_WhenThePrimaryAnswers_DoesNotCallTheBackup()
    {
        ScriptedLookup primary = new(Found);
        ScriptedLookup backup = new(FoundByTheBackup);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(Found);
        primary.Requested.Should().Equal(Cep);
        backup.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenThePrimaryIsUnavailable_UsesTheBackup()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.Unavailable);
        ScriptedLookup backup = new(FoundByTheBackup);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(FoundByTheBackup);
        backup.Requested.Should().Equal(Cep);
    }

    [Fact]
    public async Task LookupAsync_WhenThePrimaryIsUnavailableAndTheBackupDoesNotKnowTheCep_AnswersNotFound()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.Unavailable);
        ScriptedLookup backup = new(PostalCodeLookupResult.NotFound);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    [Fact]
    public async Task LookupAsync_WhenThePrimaryDoesNotKnowTheCep_StillAsksTheBackupOnce()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.NotFound);
        ScriptedLookup backup = new(FoundByTheBackup);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(FoundByTheBackup);
        primary.Requested.Should().Equal(Cep);
        backup.Requested.Should().Equal(Cep);
    }

    [Fact]
    public async Task LookupAsync_WhenBothDoNotKnowTheCep_AnswersNotFound()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.NotFound);
        ScriptedLookup backup = new(PostalCodeLookupResult.NotFound);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    [Fact]
    public async Task LookupAsync_WhenThePrimarySaysNotFoundAndTheBackupIsUnavailable_AnswersUnavailableBecauseTheNotFoundMayBeAProviderFailure()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.NotFound);
        ScriptedLookup backup = new(PostalCodeLookupResult.Unavailable);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Fact]
    public async Task LookupAsync_WhenBothAreUnavailable_AnswersUnavailable()
    {
        ScriptedLookup primary = new(PostalCodeLookupResult.Unavailable);
        ScriptedLookup backup = new(PostalCodeLookupResult.Unavailable);

        PostalCodeLookupResult result = await new FallbackPostalCodeLookup(primary, backup).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }
}
