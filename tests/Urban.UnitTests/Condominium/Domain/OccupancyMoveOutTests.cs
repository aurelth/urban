using FluentAssertions;
using Urban.Condominium.Domain.Occupancies;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>Sair da unidade: preencher a data de saída, nunca apagar o vínculo.</summary>
public class OccupancyMoveOutTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private static Occupancy Active(DateOnly? start = null) =>
        ResidentBuilders.Occupancy(ResidentBuilders.Resident(), startDate: start ?? new DateOnly(2024, 3, 1));

    [Fact]
    public void MoveOut_FillsTheEndDate_AndTheOccupancyIsNoLongerActive()
    {
        Occupancy occupancy = Active();

        Result result = occupancy.MoveOut(new DateOnly(2026, 10, 31), Today);

        result.IsSuccess.Should().BeTrue();
        occupancy.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        occupancy.IsActive.Should().BeFalse();
        occupancy.StartDate.Should().Be(new DateOnly(2024, 3, 1));
    }

    [Fact]
    public void MoveOut_AcceptsTheStartDateAndToday()
    {
        Active(new DateOnly(2026, 5, 1)).MoveOut(new DateOnly(2026, 5, 1), Today).IsSuccess.Should().BeTrue();
        Active().MoveOut(Today, Today).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void MoveOut_BeforeTheStartDate_Fails_AndStaysActive()
    {
        Occupancy occupancy = Active(new DateOnly(2026, 5, 1));

        Result result = occupancy.MoveOut(new DateOnly(2026, 4, 30), Today);

        result.Error.Should().Be(OccupancyErrors.EndDateInvalid);
        OccupancyErrors.EndDateInvalid.Code.Should().Be("condominium.occupancy.end_date_invalid");
        occupancy.IsActive.Should().BeTrue();
    }

    [Fact]
    public void MoveOut_InTheFuture_Fails()
    {
        Active().MoveOut(Today.AddDays(1), Today).Error.Should().Be(OccupancyErrors.EndDateInvalid);
    }

    [Fact]
    public void MoveOut_WithoutADate_Fails()
    {
        Active().MoveOut(null, Today).Error.Should().Be(OccupancyErrors.EndDateInvalid);
    }

    [Fact]
    public void MoveOut_OfAnOccupancyThatAlreadyEnded_Fails_AndKeepsTheFirstEndDate()
    {
        Occupancy occupancy = Active();
        occupancy.MoveOut(new DateOnly(2026, 1, 31), Today);

        Result result = occupancy.MoveOut(new DateOnly(2026, 2, 28), Today);

        result.Error.Should().Be(OccupancyErrors.AlreadyEnded);
        OccupancyErrors.AlreadyEnded.Code.Should().Be("condominium.occupancy.already_ended");
        occupancy.EndDate.Should().Be(new DateOnly(2026, 1, 31));
    }

    [Fact]
    public void TheNewErrorsHaveTheirCodes()
    {
        OccupancyErrors.AlreadyActive.Code.Should().Be("condominium.occupancy.already_active");
        OccupancyErrors.NotFound.Code.Should().Be("condominium.occupancy.not_found");
    }
}
