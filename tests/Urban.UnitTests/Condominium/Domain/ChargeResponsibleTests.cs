using FluentAssertions;
using Urban.Condominium.Domain.Occupancies;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>Quem recebe a fatura da unidade: o inquilino que mora hoje; se não houver, o proprietário que mora ou possui hoje.</summary>
public class ChargeResponsibleTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherOwner = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static ResponsibleCandidate Candidate(Guid person, OccupancyKind kind, DateOnly start, DateOnly? end = null) => new(person, kind, start, end);

    [Fact]
    public void Choose_WithoutAnyOccupancy_ReturnsNobody()
    {
        ChargeResponsible.Choose([], Today).Should().BeNull();
    }

    [Fact]
    public void Choose_ATenantWinsOverTheOwner()
    {
        ResponsibleCandidate[] candidates =
        [
            Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
            Candidate(Tenant, OccupancyKind.Tenant, new DateOnly(2025, 3, 1)),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Tenant);
    }

    [Fact]
    public void Choose_WithoutATenant_TheOwnerIsTheResponsible()
    {
        ResponsibleCandidate[] candidates = [Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1))];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Owner);
    }

    [Fact]
    public void Choose_ADependentNeverReceivesTheInvoice()
    {
        ResponsibleCandidate[] candidates = [Candidate(Guid.CreateVersion7(), OccupancyKind.Dependent, new DateOnly(2024, 1, 1))];

        ChargeResponsible.Choose(candidates, Today).Should().BeNull();
    }

    [Fact]
    public void Choose_ATenantWhoAlreadyLeftIsIgnored()
    {
        ResponsibleCandidate[] candidates =
        [
            Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
            Candidate(Tenant, OccupancyKind.Tenant, new DateOnly(2024, 1, 1), new DateOnly(2026, 10, 31)),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Owner);
    }

    [Fact]
    public void Choose_AnOccupancyThatEndsToday_StillCounts()
    {
        ResponsibleCandidate[] candidates =
        [
            Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
            Candidate(Tenant, OccupancyKind.Tenant, new DateOnly(2024, 1, 1), Today),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Tenant);
    }

    [Fact]
    public void Choose_AnOccupancyThatHasNotStartedYet_IsIgnored()
    {
        ResponsibleCandidate[] candidates =
        [
            Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
            Candidate(Tenant, OccupancyKind.Tenant, Today.AddDays(1)),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Owner);
    }

    [Fact]
    public void Choose_AnOccupancyThatStartsToday_Counts()
    {
        ResponsibleCandidate[] candidates = [Candidate(Tenant, OccupancyKind.Tenant, Today)];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Tenant);
    }

    [Fact]
    public void Choose_WithTwoTenants_TheMostRecentOneWins()
    {
        Guid newest = Guid.Parse("00000000-0000-0000-0000-000000000009");
        ResponsibleCandidate[] candidates =
        [
            Candidate(Tenant, OccupancyKind.Tenant, new DateOnly(2024, 1, 1)),
            Candidate(newest, OccupancyKind.Tenant, new DateOnly(2026, 2, 1)),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(newest);
    }

    [Fact]
    public void Choose_WithTwoOwnersStartingTheSameDay_ChoosesAlwaysTheSamePerson()
    {
        ResponsibleCandidate[] candidates =
        [
            Candidate(OtherOwner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
            Candidate(Owner, OccupancyKind.Owner, new DateOnly(2020, 1, 1)),
        ];

        ChargeResponsible.Choose(candidates, Today).Should().Be(Owner);
        ChargeResponsible.Choose([.. candidates.Reverse()], Today).Should().Be(Owner);
    }
}
